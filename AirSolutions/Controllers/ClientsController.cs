using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AirSolutions.Data;
using AirSolutions.Models;

namespace AirSolutions.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ClientsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista clientes con búsqueda opcional por nombre o teléfono.</summary>
    [HttpGet]
    public async Task<ActionResult<List<Client>>> GetClients(
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Clients.AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                (c.FirstName != null && c.FirstName.ToLower().Contains(term)) ||
                (c.LastName != null && c.LastName.ToLower().Contains(term)) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)) ||
                (c.SecondaryPhone != null && c.SecondaryPhone.Contains(term)));
        }

        var list = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Client>> GetClient(int id, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client == null) return NotFound();
        return Ok(client);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Client>> CreateClient([FromBody] Client model, CancellationToken cancellationToken = default)
    {
        var errors = ValidateClient(model, isNew: true);
        if (errors.Count > 0) return BadRequest(new { errors });

        model.Id = 0;
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = null;

        _db.Clients.Add(model);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetClient), new { id = model.Id }, model);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Client>> UpdateClient(int id, [FromBody] Client model, CancellationToken cancellationToken = default)
    {
        if (id != model.Id) return BadRequest();
        var existing = await _db.Clients.FindAsync([id], cancellationToken);
        if (existing == null) return NotFound();

        var errors = ValidateClient(model, isNew: false);
        if (errors.Count > 0) return BadRequest(new { errors });

        existing.ClientType = model.ClientType;
        existing.FirstName = model.FirstName;
        existing.LastName = model.LastName;
        existing.CompanyName = model.CompanyName;
        existing.DocumentNumber = model.DocumentNumber;
        existing.Phone = model.Phone;
        existing.SecondaryPhone = model.SecondaryPhone;
        existing.Email = model.Email;
        existing.Address = model.Address;
        existing.Sector = model.Sector;
        existing.City = model.City;
        existing.Notes = model.Notes;
        existing.PreferredPaymentMethod = model.PreferredPaymentMethod;
        existing.IsActive = model.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(existing);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteClient(int id, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FindAsync([id], cancellationToken);
        if (client == null) return NotFound();
        _db.Clients.Remove(client);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static List<string> ValidateClient(Client m, bool isNew)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(m.ClientType))
            errors.Add("ClientType es obligatorio.");
        else if (m.ClientType != "Individual" && m.ClientType != "Company")
            errors.Add("ClientType debe ser 'Individual' o 'Company'.");
        if (string.IsNullOrWhiteSpace(m.FirstName))
            errors.Add("FirstName es obligatorio.");
        if (m.ClientType == "Company" && string.IsNullOrWhiteSpace(m.CompanyName))
            errors.Add("CompanyName es obligatorio cuando el tipo es Company.");
        if (string.IsNullOrWhiteSpace(m.Phone))
            errors.Add("Phone es obligatorio.");
        if (isNew && m.CreatedAt == default)
            m.CreatedAt = DateTime.UtcNow;
        return errors;
    }
}
