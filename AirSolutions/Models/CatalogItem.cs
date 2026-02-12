namespace AirSolutions.Models;

public class CatalogItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string ItemType { get; set; } = ""; // "Service" | "Product" | "Material" | "Other"
    public string? Nivel { get; set; } // Solo necesario si ItemType = "Service"
    public string? SKU { get; set; }
    public string? Unit { get; set; }
    public decimal? BasePrice { get; set; }
    public decimal? Cost { get; set; }
    public bool IsTaxable { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
