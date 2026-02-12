namespace AirSolutions.Models;

public class QuoteLine
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote? Quote { get; set; }

    // Datos del item (copiados del catálogo o escritos a mano, no FK de precio)
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    // Descuento
    // DiscountValue se interpretará como porcentaje (0-100)
    public decimal DiscountValue { get; set; }
    public decimal DiscountTotal { get; set; }

    // Impuestos
    public bool IsTaxable { get; set; }
    // TaxRate también como porcentaje (0-100)
    public decimal TaxRate { get; set; }
    public decimal TaxTotal { get; set; }

    // Totales de la línea
    public decimal LineSubtotal { get; set; }
    public decimal LineTotal { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

