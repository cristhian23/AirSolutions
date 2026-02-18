using AirSolutions.Data;
using AirSolutions.Models;
using AirSolutions.Models.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AirSolutions.Services;

public class AuthBootstrapper
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly AuthBootstrapSettings _settings;

    public AuthBootstrapper(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        IOptions<AuthBootstrapSettings> settings)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _settings = settings.Value;
    }

    public async Task EnsureSeedAdminAsync(CancellationToken cancellationToken = default)
    {
        var username = (_settings.SeedAdminUsername ?? "cristhian").Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            username = "cristhian";
        }

        var existing = await _db.Users.FirstOrDefaultAsync(
            u => u.Username == username,
            cancellationToken);

        if (existing != null)
        {
            if (!existing.IsActive || !string.Equals(existing.Role, _settings.SeedAdminRole, StringComparison.OrdinalIgnoreCase))
            {
                existing.IsActive = true;
                existing.Role = string.IsNullOrWhiteSpace(_settings.SeedAdminRole) ? "Admin" : _settings.SeedAdminRole.Trim();
                existing.FullName = string.IsNullOrWhiteSpace(existing.FullName)
                    ? (_settings.SeedAdminFullName ?? "Cristhian Cuevas")
                    : existing.FullName;
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var user = new User
        {
            Username = username,
            FullName = string.IsNullOrWhiteSpace(_settings.SeedAdminFullName) ? "Cristhian Cuevas" : _settings.SeedAdminFullName.Trim(),
            Role = string.IsNullOrWhiteSpace(_settings.SeedAdminRole) ? "Admin" : _settings.SeedAdminRole.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var seedPasswordFromEnv = Environment.GetEnvironmentVariable("AUTH_SEED_ADMIN_PASSWORD");
        var password = string.IsNullOrWhiteSpace(seedPasswordFromEnv)
            ? (_settings.SeedAdminPassword ?? "")
            : seedPasswordFromEnv.Trim();

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("No hay password de seed para usuario admin. Define AUTH_SEED_ADMIN_PASSWORD o AuthBootstrap:SeedAdminPassword.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
