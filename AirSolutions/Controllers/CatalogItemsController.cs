using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AirSolutions.Data;
using AirSolutions.Models;

namespace AirSolutions.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogItemsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CatalogItemsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista items del catálogo con búsqueda opcional por nombre, descripción o SKU.</summary>
    [HttpGet]
    public async Task<ActionResult<List<CatalogItem>>> GetCatalogItems(
        [FromQuery] string? search = null,
        [FromQuery] string? itemType = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.CatalogItems.AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(itemType))
            query = query.Where(c => c.ItemType == itemType);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                (c.Name != null && c.Name.ToLower().Contains(term)) ||
                (c.Description != null && c.Description.ToLower().Contains(term)) ||
                (c.SKU != null && c.SKU.ToLower().Contains(term)));
        }

        var list = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatalogItem>> GetCatalogItem(int id, CancellationToken cancellationToken = default)
    {
        var item = await _db.CatalogItems.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CatalogItem>> CreateCatalogItem([FromBody] CatalogItem model, CancellationToken cancellationToken = default)
    {
        var errors = ValidateCatalogItem(model, isNew: true);
        if (errors.Count > 0) return BadRequest(new { errors });

        model.Id = 0;
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = null;

        _db.CatalogItems.Add(model);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetCatalogItem), new { id = model.Id }, model);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CatalogItem>> UpdateCatalogItem(int id, [FromBody] CatalogItem model, CancellationToken cancellationToken = default)
    {
        if (id != model.Id) return BadRequest();
        var existing = await _db.CatalogItems.FindAsync([id], cancellationToken);
        if (existing == null) return NotFound();

        var errors = ValidateCatalogItem(model, isNew: false);
        if (errors.Count > 0) return BadRequest(new { errors });

        existing.Name = model.Name;
        existing.Description = model.Description;
        existing.ItemType = model.ItemType;
        existing.Nivel = model.Nivel;
        existing.SKU = model.SKU;
        existing.Unit = model.Unit;
        existing.BasePrice = model.BasePrice;
        existing.Cost = model.Cost;
        existing.IsTaxable = model.IsTaxable;
        existing.IsActive = model.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(existing);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteCatalogItem(int id, CancellationToken cancellationToken = default)
    {
        var item = await _db.CatalogItems.FindAsync([id], cancellationToken);
        if (item == null) return NotFound();
        _db.CatalogItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static List<string> ValidateCatalogItem(CatalogItem m, bool isNew)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(m.Name))
            errors.Add("Name es obligatorio.");
        if (string.IsNullOrWhiteSpace(m.ItemType))
            errors.Add("ItemType es obligatorio.");
        else if (!new[] { "Service", "Product", "Material", "Other" }.Contains(m.ItemType))
            errors.Add("ItemType debe ser 'Service', 'Product', 'Material' o 'Other'.");
        if (m.ItemType == "Service" && string.IsNullOrWhiteSpace(m.Nivel))
            errors.Add("Nivel es obligatorio cuando el tipo es Service.");
        if (isNew && m.CreatedAt == default)
            m.CreatedAt = DateTime.UtcNow;
        return errors;
    }
}
