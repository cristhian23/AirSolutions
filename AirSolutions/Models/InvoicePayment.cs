namespace AirSolutions.Models;

public class InvoicePayment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }

    public string Method { get; set; } = "";
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
}
