using AirSolutions.Data;
using AirSolutions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirSolutions.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public InvoicesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public class InvoiceLineRequest
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountValue { get; set; }
        public bool IsTaxable { get; set; }
        public decimal TaxRate { get; set; }
    }

    public class InvoiceCreateRequest
    {
        public int? QuoteId { get; set; }
        public int? ClientId { get; set; }
        public string? Description { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public bool RequiresFiscalVoucher { get; set; }
        public List<InvoiceLineRequest> Lines { get; set; } = new();
    }

    public class AddPaymentRequest
    {
        public DateTime? PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "";
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetInvoices(
        [FromQuery] string? search = null,
        [FromQuery] int? clientId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Invoices
            .Include(i => i.Client)
            .Include(i => i.FiscalVoucher)
            .AsNoTracking();

        if (clientId.HasValue)
            query = query.Where(i => i.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(i => i.Status == status.Trim());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceCode.ToLower().Contains(term) ||
                (i.Description != null && i.Description.ToLower().Contains(term)) ||
                (i.Client != null && (
                    (i.Client.FirstName != null && i.Client.FirstName.ToLower().Contains(term)) ||
                    (i.Client.LastName != null && i.Client.LastName.ToLower().Contains(term)) ||
                    (i.Client.CompanyName != null && i.Client.CompanyName.ToLower().Contains(term)))
                ) ||
                (i.FiscalVoucher != null && i.FiscalVoucher.VoucherNumber.ToLower().Contains(term))
            );
        }

        var list = await query
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.InvoiceCode,
                i.Description,
                i.IssueDate,
                i.DueDate,
                i.Status,
                i.RequiresFiscalVoucher,
                i.Subtotal,
                i.DiscountTotal,
                i.TaxTotal,
                i.GrandTotal,
                i.PaidTotal,
                i.BalanceDue,
                i.CreatedAt,
                i.UpdatedAt,
                Client = i.Client == null ? null : new
                {
                    i.Client.Id,
                    i.Client.ClientType,
                    i.Client.FirstName,
                    i.Client.LastName,
                    i.Client.CompanyName
                },
                FiscalVoucher = i.FiscalVoucher == null ? null : new
                {
                    i.FiscalVoucher.Id,
                    i.FiscalVoucher.VoucherNumber,
                    i.FiscalVoucher.VoucherType,
                    i.FiscalVoucher.IsUsed,
                    i.FiscalVoucher.UsedAt,
                    i.FiscalVoucher.UsedInInvoiceId
                }
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> GetInvoice(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Client)
            .Include(i => i.FiscalVoucher)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice == null) return NotFound();

        return Ok(new
        {
            invoice.Id,
            invoice.QuoteId,
            invoice.ClientId,
            Client = invoice.Client == null ? null : new
            {
            invoice.Client.Id,
                invoice.Client.ClientType,
                invoice.Client.FirstName,
                invoice.Client.LastName,
                invoice.Client.CompanyName,
                invoice.Client.Phone,
                invoice.Client.Email
            },
            invoice.InvoiceCode,
            invoice.Description,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.Status,
            invoice.RequiresFiscalVoucher,
            invoice.FiscalVoucherId,
            FiscalVoucher = invoice.FiscalVoucher == null ? null : new
            {
                invoice.FiscalVoucher.Id,
                invoice.FiscalVoucher.VoucherNumber,
                invoice.FiscalVoucher.VoucherType,
                invoice.FiscalVoucher.IsUsed,
                invoice.FiscalVoucher.UsedAt,
                invoice.FiscalVoucher.UsedInInvoiceId
            },
            invoice.Subtotal,
            invoice.DiscountTotal,
            invoice.TaxTotal,
            invoice.GrandTotal,
            invoice.PaidTotal,
            invoice.BalanceDue,
            invoice.CreatedAt,
            invoice.UpdatedAt,
            Lines = invoice.Lines
                .OrderBy(x => x.Id)
                .Select(l => new
                {
                    l.Id,
                    l.InvoiceId,
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
                }).ToList(),
            Payments = invoice.Payments
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new
                {
                    p.Id,
                    p.InvoiceId,
                    p.PaymentDate,
                    p.Amount,
                    p.Method,
                    p.Reference,
                    p.Notes,
                    p.CreatedAt
                }).ToList()
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> CreateInvoice([FromBody] InvoiceCreateRequest request, CancellationToken cancellationToken = default)
    {
        var errors = await ValidateInvoiceRequest(request, null, cancellationToken);
        if (errors.Count > 0) return BadRequest(new { errors });

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = new Invoice
        {
            QuoteId = request.QuoteId,
            ClientId = request.ClientId!.Value,
            InvoiceCode = string.Empty,
            Description = request.Description,
            IssueDate = request.IssueDate?.Date ?? DateTime.UtcNow.Date,
            DueDate = request.DueDate?.Date,
            Status = "Draft",
            RequiresFiscalVoucher = request.RequiresFiscalVoucher,
            FiscalVoucherId = null,
            CreatedAt = DateTime.UtcNow
        };

        if (invoice.RequiresFiscalVoucher)
        {
            var voucher = await _db.FiscalVouchers
                .Where(v => !v.IsUsed)
                .OrderBy(v => v.VoucherNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (voucher == null)
            {
                return BadRequest(new { errors = new[] { "No hay comprobantes fiscales disponibles." } });
            }

            invoice.FiscalVoucherId = voucher.Id;
        }

        foreach (var line in request.Lines)
        {
            invoice.Lines.Add(MapLine(line));
        }

        RecalculateInvoiceTotals(invoice);

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        invoice.InvoiceCode = $"FACTURA-{invoice.Id}";
        await _db.SaveChangesAsync(cancellationToken);

        if (invoice.RequiresFiscalVoucher && invoice.FiscalVoucherId.HasValue)
        {
            var voucher = await _db.FiscalVouchers.FirstAsync(v => v.Id == invoice.FiscalVoucherId.Value, cancellationToken);
            voucher.IsUsed = true;
            voucher.UsedAt = DateTime.UtcNow;
            voucher.UsedInInvoiceId = invoice.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, new { invoice.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> UpdateInvoice(int id, [FromBody] InvoiceCreateRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice == null) return NotFound();

        var errors = await ValidateInvoiceRequest(request, id, cancellationToken);
        if (errors.Count > 0) return BadRequest(new { errors });

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        invoice.QuoteId = request.QuoteId;
        invoice.ClientId = request.ClientId!.Value;
        invoice.Description = request.Description;
        invoice.IssueDate = request.IssueDate?.Date ?? invoice.IssueDate;
        invoice.DueDate = request.DueDate?.Date;
        invoice.UpdatedAt = DateTime.UtcNow;

        _db.InvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines.Clear();
        foreach (var line in request.Lines)
        {
            invoice.Lines.Add(MapLine(line));
        }

        RecalculateInvoiceTotals(invoice);
        UpdateStatusFromBalance(invoice);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Ok(new { invoice.Id });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteInvoice(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice == null) return NotFound();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (invoice.FiscalVoucherId.HasValue)
        {
            var voucher = await _db.FiscalVouchers.FirstOrDefaultAsync(v => v.Id == invoice.FiscalVoucherId.Value, cancellationToken);
            if (voucher != null && voucher.UsedInInvoiceId == invoice.Id)
            {
                voucher.IsUsed = false;
                voucher.UsedAt = null;
                voucher.UsedInInvoiceId = null;
            }
        }

        _db.InvoicePayments.RemoveRange(invoice.Payments);
        _db.InvoiceLines.RemoveRange(invoice.Lines);
        _db.Invoices.Remove(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/payments")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> AddPayment(int id, [FromBody] AddPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice == null) return NotFound();

        var errors = new List<string>();
        if (request.Amount <= 0) errors.Add("El monto del pago debe ser mayor que 0.");
        if (string.IsNullOrWhiteSpace(request.Method)) errors.Add("El metodo de pago es obligatorio.");
        if (invoice.Status == "Cancelled") errors.Add("No se pueden registrar pagos en una factura cancelada.");
        if (errors.Count > 0) return BadRequest(new { errors });

        var payment = new InvoicePayment
        {
            InvoiceId = invoice.Id,
            PaymentDate = request.PaymentDate?.Date ?? DateTime.UtcNow.Date,
            Amount = decimal.Round(request.Amount, 2),
            Method = request.Method.Trim(),
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.InvoicePayments.Add(payment);
        invoice.PaidTotal = decimal.Round(invoice.PaidTotal + payment.Amount, 2);
        invoice.BalanceDue = decimal.Round(invoice.GrandTotal - invoice.PaidTotal, 2);
        UpdateStatusFromBalance(invoice);
        invoice.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { PaymentId = payment.Id, InvoiceId = invoice.Id, invoice.PaidTotal, invoice.BalanceDue, invoice.Status });
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> CancelInvoice(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invoice == null) return NotFound();

        if (invoice.Status == "Cancelled") return Ok(new { invoice.Id, invoice.Status });

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        invoice.Status = "Cancelled";
        invoice.UpdatedAt = DateTime.UtcNow;

        if (invoice.FiscalVoucherId.HasValue)
        {
            var voucher = await _db.FiscalVouchers.FirstOrDefaultAsync(v => v.Id == invoice.FiscalVoucherId.Value, cancellationToken);
            if (voucher != null && voucher.UsedInInvoiceId == invoice.Id)
            {
                voucher.IsUsed = false;
                voucher.UsedAt = null;
                voucher.UsedInInvoiceId = null;
            }

            invoice.FiscalVoucherId = null;
            invoice.RequiresFiscalVoucher = false;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Ok(new { invoice.Id, invoice.Status });
    }

    private async Task<List<string>> ValidateInvoiceRequest(InvoiceCreateRequest request, int? invoiceId, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (!request.ClientId.HasValue)
        {
            errors.Add("Debe seleccionar un cliente.");
        }
        else
        {
            var exists = await _db.Clients.AnyAsync(c => c.Id == request.ClientId.Value, cancellationToken);
            if (!exists) errors.Add("El cliente seleccionado no existe.");
        }

        if (request.QuoteId.HasValue)
        {
            var quoteExists = await _db.Quotes.AnyAsync(q => q.Id == request.QuoteId.Value, cancellationToken);
            if (!quoteExists) errors.Add("La cotización base no existe.");
        }

        if (request.Lines == null || request.Lines.Count == 0)
            errors.Add("La factura debe tener al menos una línea.");

        foreach (var line in request.Lines ?? Enumerable.Empty<InvoiceLineRequest>())
        {
            errors.AddRange(ValidateLine(line));
        }

        if (request.RequiresFiscalVoucher && !invoiceId.HasValue)
        {
            var hasAvailable = await _db.FiscalVouchers.AnyAsync(v => !v.IsUsed, cancellationToken);
            if (!hasAvailable)
                errors.Add("No hay comprobantes fiscales disponibles.");
        }

        return errors;
    }

    private static List<string> ValidateLine(InvoiceLineRequest l)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(l.Name)) errors.Add("El nombre de la línea es obligatorio.");
        if (l.Quantity <= 0) errors.Add("Quantity debe ser mayor que 0.");
        if (l.UnitPrice < 0) errors.Add("UnitPrice no puede ser negativo.");
        if (l.DiscountValue < 0 || l.DiscountValue > 100) errors.Add("DiscountValue debe estar entre 0 y 100.");
        if (l.TaxRate < 0 || l.TaxRate > 100) errors.Add("TaxRate debe estar entre 0 y 100.");
        return errors;
    }

    private static InvoiceLine MapLine(InvoiceLineRequest l)
    {
        var subtotal = l.Quantity * l.UnitPrice;
        var discountTotal = subtotal * (l.DiscountValue / 100m);
        var baseAfterDiscount = subtotal - discountTotal;
        var taxTotal = l.IsTaxable ? baseAfterDiscount * (l.TaxRate / 100m) : 0m;
        var total = baseAfterDiscount + taxTotal;

        return new InvoiceLine
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

    private static void RecalculateInvoiceTotals(Invoice invoice)
    {
        invoice.Subtotal = decimal.Round(invoice.Lines.Sum(l => l.LineSubtotal), 2);
        invoice.DiscountTotal = decimal.Round(invoice.Lines.Sum(l => l.DiscountTotal), 2);
        invoice.TaxTotal = decimal.Round(invoice.Lines.Sum(l => l.TaxTotal), 2);
        invoice.GrandTotal = decimal.Round(invoice.Lines.Sum(l => l.LineTotal), 2);
        invoice.BalanceDue = decimal.Round(invoice.GrandTotal - invoice.PaidTotal, 2);
    }

    private static void UpdateStatusFromBalance(Invoice invoice)
    {
        if (invoice.Status == "Cancelled") return;

        if (invoice.PaidTotal <= 0)
        {
            invoice.Status = "Sent";
            return;
        }

        invoice.Status = invoice.BalanceDue <= 0 ? "Paid" : "PartiallyPaid";
    }
}
