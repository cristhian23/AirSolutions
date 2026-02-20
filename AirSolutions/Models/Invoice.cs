namespace AirSolutions.Models;

public class Invoice
{
    public int Id { get; set; }

    public int? QuoteId { get; set; }
    public Quote? Quote { get; set; }

    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public string InvoiceCode { get; set; } = "";
    public string? Description { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Draft";

    public bool RequiresFiscalVoucher { get; set; }
    public int? FiscalVoucherId { get; set; }
    public FiscalVoucher? FiscalVoucher { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidTotal { get; set; }
    public decimal BalanceDue { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
}
