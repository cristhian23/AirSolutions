using AirSolutions.Data;
using AirSolutions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirSolutions.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FiscalVouchersController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public FiscalVouchersController(ApplicationDbContext db)
    {
        _db = db;
    }

    public class FiscalVoucherCreateRequest
    {
        public string VoucherNumber { get; set; } = "";
        public string? VoucherType { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> Get(
        [FromQuery] bool onlyAvailable = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.FiscalVouchers.AsNoTracking();
        if (onlyAvailable)
            query = query.Where(v => !v.IsUsed);

        var list = await query
            .OrderBy(v => v.VoucherNumber)
            .Select(v => new
            {
                v.Id,
                v.VoucherNumber,
                v.VoucherType,
                v.IsUsed,
                v.UsedAt,
                v.UsedInInvoiceId,
                v.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> Create([FromBody] FiscalVoucherCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.VoucherNumber))
            return BadRequest(new { errors = new[] { "VoucherNumber es obligatorio." } });

        var voucherNumber = request.VoucherNumber.Trim();
        var exists = await _db.FiscalVouchers.AnyAsync(v => v.VoucherNumber == voucherNumber, cancellationToken);
        if (exists)
            return Conflict(new { message = "Ese comprobante ya existe." });

        var voucher = new FiscalVoucher
        {
            VoucherNumber = voucherNumber,
            VoucherType = string.IsNullOrWhiteSpace(request.VoucherType) ? null : request.VoucherType.Trim(),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.FiscalVouchers.Add(voucher);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = voucher.Id }, new
        {
            voucher.Id,
            voucher.VoucherNumber,
            voucher.VoucherType,
            voucher.IsUsed,
            voucher.CreatedAt
        });
    }
}
