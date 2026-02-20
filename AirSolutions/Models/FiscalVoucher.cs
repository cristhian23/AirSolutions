namespace AirSolutions.Models;

public class FiscalVoucher
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = "";
    public string? VoucherType { get; set; }

    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public int? UsedInInvoiceId { get; set; }
    public Invoice? UsedInInvoice { get; set; }

    public DateTime CreatedAt { get; set; }
}
