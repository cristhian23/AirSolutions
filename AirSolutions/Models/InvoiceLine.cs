namespace AirSolutions.Models;

public class InvoiceLine
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal DiscountValue { get; set; }
    public decimal DiscountTotal { get; set; }

    public bool IsTaxable { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxTotal { get; set; }

    public decimal LineSubtotal { get; set; }
    public decimal LineTotal { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
