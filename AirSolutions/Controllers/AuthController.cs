using AirSolutions.Models.Auth;
using AirSolutions.Models;
using AirSolutions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace AirSolutions.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtSettings _jwtSettings;
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthController(
        IOptions<JwtSettings> jwtOptions,
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher)
    {
        _jwtSettings = jwtOptions.Value;
        _db = db;
        _passwordHasher = passwordHasher;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Usuario y contraseña son obligatorios." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Username == request.Username.Trim(),
            cancellationToken);

        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiresMinutes <= 0 ? 480 : _jwtSettings.ExpiresMinutes);
        var jwtKey = ResolveJwtKey();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName),
            new Claim(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? "User" : user.Role)
        };

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

        return Ok(new LoginResponse
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAtUtc,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role
        });
    }

    private string ResolveJwtKey()
    {
        var envKey = Environment.GetEnvironmentVariable("JWT_KEY");
        var key = string.IsNullOrWhiteSpace(envKey) ? _jwtSettings.Key : envKey.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        {
            throw new InvalidOperationException("JWT key no configurada o muy corta. Define JWT_KEY con al menos 32 caracteres.");
        }

        return key;
    }
}

