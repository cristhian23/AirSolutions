using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AirSolutions.Data;
using AirSolutions.Models;

namespace AirSolutions.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public QuotesController(ApplicationDbContext db)
    {
        _db = db;
    }

    // DTOs de entrada/salida
    public class QuoteLineRequest
    {
        public int? Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountValue { get; set; } // porcentaje 0-100
        public bool IsTaxable { get; set; }
        public decimal TaxRate { get; set; } // porcentaje 0-100
    }

    public class QuoteCreateRequest
    {
        public int? FromQuoteId { get; set; }

        public int? ClientId { get; set; }
        public Client? NewClient { get; set; }

        public string? Name { get; set; }
        public string? Description { get; set; }

        public List<QuoteLineRequest> Lines { get; set; } = new();
    }

    /// <summary>Lista cotizaciones con búsqueda opcional por nombre, descripción o cliente.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetQuotes(
        [FromQuery] string? search = null,
        [FromQuery] int? clientId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Quotes
            .Include(q => q.Client)
            .AsNoTracking();

        if (clientId.HasValue)
            query = query.Where(q => q.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(q =>
                (q.Name != null && q.Name.ToLower().Contains(term)) ||
                (q.Description != null && q.Description.ToLower().Contains(term)) ||
                (q.Client != null &&
                    (
                        (q.Client.FirstName != null && q.Client.FirstName.ToLower().Contains(term)) ||
                        (q.Client.LastName != null && q.Client.LastName.ToLower().Contains(term)) ||
                        (q.Client.CompanyName != null && q.Client.CompanyName.ToLower().Contains(term))
                    )));
        }

        var list = await query
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new
            {
                q.Id,
                q.Name,
                q.Description,
                q.CreatedAt,
                q.UpdatedAt,
                Client = q.Client == null
                    ? null
                    : new
                    {
                        q.Client.Id,
                        q.Client.ClientType,
                        q.Client.FirstName,
                        q.Client.LastName,
                        q.Client.CompanyName
                    }
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    /// <summary>Obtiene una cotización con sus líneas.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> GetQuote(int id, CancellationToken cancellationToken = default)
    {
        var quote = await _db.Quotes
            .Include(q => q.Client)
            .Include(q => q.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quote == null) return NotFound();

        var result = new
        {
            quote.Id,
            quote.ClientId,
            Client = quote.Client == null
                ? null
                : new
                {
                    quote.Client.Id,
                    quote.Client.ClientType,
                    quote.Client.FirstName,
                    quote.Client.LastName,
                    quote.Client.CompanyName
                },
            quote.Name,
            quote.Description,
            quote.CreatedAt,
            quote.UpdatedAt,
            Lines = quote.Lines
                .OrderBy(l => l.Id)
                .Select(l => new
                {
                    l.Id,
                    l.QuoteId,
                    l.Name,
                    l.Description,
                    l.Quantity,
                    l.UnitPrice,
                    l.DiscountValue,
                    l.DiscountTotal,
                    l.IsTaxable,
                    l.TaxRate,
                    l.TaxTotal,
                    l.LineSubtotal,
                    l.LineTotal,
                    l.CreatedAt,
                    l.UpdatedAt
                }).ToList()
        };

        return Ok(result);
    }

    /// <summary>Crea una cotización nueva (desde cero o desde plantilla).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> CreateQuote([FromBody] QuoteCreateRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Cliente: usar existente o crear uno nuevo en el mismo flujo
        int clientId;
        if (request.ClientId.HasValue)
        {
            var clientExists = await _db.Clients.AnyAsync(c => c.Id == request.ClientId.Value, cancellationToken);
            if (!clientExists)
                errors.Add("El cliente seleccionado no existe.");
            clientId = request.ClientId.Value;
        }
        else if (request.NewClient != null)
        {
            var clientErrors = ValidateClientForInlineCreate(request.NewClient);
            if (clientErrors.Count > 0)
            {
                errors.AddRange(clientErrors);
                return BadRequest(new { errors });
            }

            request.NewClient.CreatedAt = DateTime.UtcNow;
            _db.Clients.Add(request.NewClient);
            await _db.SaveChangesAsync(cancellationToken);
            clientId = request.NewClient.Id;
        }
        else
        {
            errors.Add("Debe seleccionar un cliente existente o crear uno nuevo.");
            return BadRequest(new { errors });
        }

        if (request.Lines == null || request.Lines.Count == 0)
            errors.Add("La cotización debe tener al menos una línea.");

        if (errors.Count > 0)
            return BadRequest(new { errors });

        var quote = new Quote
        {
            ClientId = clientId,
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        // Si viene plantilla, podemos copiar campos por defecto (nombre/descripción/líneas) y luego aplicar overrides
        if (request.FromQuoteId.HasValue)
        {
            var baseQuote = await _db.Quotes
                .Include(q => q.Lines)
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == request.FromQuoteId.Value, cancellationToken);

            if (baseQuote != null)
            {
                if (string.IsNullOrWhiteSpace(quote.Name))
                    quote.Name = baseQuote.Name;
                if (string.IsNullOrWhiteSpace(quote.Description))
                    quote.Description = baseQuote.Description;

                foreach (var l in baseQuote.Lines)
                {
                    quote.Lines.Add(new QuoteLine
                    {
                        Name = l.Name,
                        Description = l.Description,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        DiscountValue = l.DiscountValue,
                        DiscountTotal = l.DiscountTotal,
                        IsTaxable = l.IsTaxable,
                        TaxRate = l.TaxRate,
                        TaxTotal = l.TaxTotal,
                        LineSubtotal = l.LineSubtotal,
                        LineTotal = l.LineTotal,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // Si vienen líneas explícitas en la solicitud, usamos esas (sobrescriben cualquier plantilla)
        if (request.Lines.Count > 0)
        {
            quote.Lines.Clear();
            foreach (var lineReq in request.Lines)
            {
                var lineErrors = ValidateLine(lineReq);
                if (lineErrors.Count > 0)
                {
                    errors.AddRange(lineErrors);
                    return BadRequest(new { errors });
                }

                var line = MapLine(lineReq);
                quote.Lines.Add(line);
            }
        }

        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetQuote), new { id = quote.Id }, new { quote.Id });
    }

    /// <summary>Actualiza una cotización (cabecera + líneas).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> UpdateQuote(int id, [FromBody] QuoteCreateRequest request, CancellationToken cancellationToken = default)
    {
        var quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quote == null) return NotFound();

        var errors = new List<string>();

        // Cliente (solo se permite cambiar a otro existente; creación inline solo en POST)
        if (request.ClientId.HasValue)
        {
            var clientExists = await _db.Clients.AnyAsync(c => c.Id == request.ClientId.Value, cancellationToken);
            if (!clientExists)
                errors.Add("El cliente seleccionado no existe.");
            else
                quote.ClientId = request.ClientId.Value;
        }

        if (request.NewClient != null)
            errors.Add("No se permite crear un cliente nuevo en la edición de una cotización. Cree el cliente antes y selecciónelo.");

        if (request.Lines == null || request.Lines.Count == 0)
            errors.Add("La cotización debe tener al menos una línea.");

        if (errors.Count > 0)
            return BadRequest(new { errors });

        quote.Name = request.Name;
        quote.Description = request.Description;
        quote.UpdatedAt = DateTime.UtcNow;

        // Reemplazar todas las líneas por las enviadas
        _db.QuoteLines.RemoveRange(quote.Lines);
        quote.Lines.Clear();

        foreach (var lineReq in request.Lines)
        {
            var lineErrors = ValidateLine(lineReq);
            if (lineErrors.Count > 0)
            {
                errors.AddRange(lineErrors);
                return BadRequest(new { errors });
            }

            var line = MapLine(lineReq);
            quote.Lines.Add(line);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { quote.Id });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteQuote(int id, CancellationToken cancellationToken = default)
    {
        var quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (quote == null) return NotFound();

        _db.QuoteLines.RemoveRange(quote.Lines);
        _db.Quotes.Remove(quote);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // --- Helpers ---

    private static List<string> ValidateClientForInlineCreate(Client c)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(c.ClientType))
            errors.Add("ClientType es obligatorio para el nuevo cliente.");
        if (string.IsNullOrWhiteSpace(c.FirstName))
            errors.Add("FirstName es obligatorio para el nuevo cliente.");
        if (string.IsNullOrWhiteSpace(c.Phone))
            errors.Add("Phone es obligatorio para el nuevo cliente.");
        if (c.ClientType == "Company" && string.IsNullOrWhiteSpace(c.CompanyName))
            errors.Add("CompanyName es obligatorio cuando el tipo es Company.");
        return errors;
    }

    private static List<string> ValidateLine(QuoteLineRequest l)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(l.Name))
            errors.Add("El nombre de la línea es obligatorio.");
        if (l.Quantity <= 0)
            errors.Add("Quantity debe ser mayor que 0.");
        if (l.UnitPrice < 0)
            errors.Add("UnitPrice no puede ser negativo.");
        if (l.DiscountValue < 0 || l.DiscountValue > 100)
            errors.Add("DiscountValue debe estar entre 0 y 100 (porcentaje).");
        if (l.TaxRate < 0 || l.TaxRate > 100)
            errors.Add("TaxRate debe estar entre 0 y 100 (porcentaje).");
        return errors;
    }

    private static QuoteLine MapLine(QuoteLineRequest l)
    {
        var subtotal = l.Quantity * l.UnitPrice;
        var discountTotal = subtotal * (l.DiscountValue / 100m);
        var baseAfterDiscount = subtotal - discountTotal;
        var taxTotal = l.IsTaxable ? baseAfterDiscount * (l.TaxRate / 100m) : 0m;
        var total = baseAfterDiscount + taxTotal;

        return new QuoteLine
        {
            Name = l.Name,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            DiscountValue = l.DiscountValue,
            DiscountTotal = decimal.Round(discountTotal, 2),
            IsTaxable = l.IsTaxable,
            TaxRate = l.TaxRate,
            TaxTotal = decimal.Round(taxTotal, 2),
            LineSubtotal = decimal.Round(subtotal, 2),
            LineTotal = decimal.Round(total, 2),
            CreatedAt = DateTime.UtcNow
        };
    }
}


