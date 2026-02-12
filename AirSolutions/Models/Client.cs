namespace AirSolutions.Models;

public class Client
{
    public int Id { get; set; }
    public string ClientType { get; set; } = ""; // "Individual" | "Company"
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? DocumentNumber { get; set; }
    public string Phone { get; set; } = "";
    public string? SecondaryPhone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Sector { get; set; }
    public string? City { get; set; }
    public string? Notes { get; set; }
    public string? PreferredPaymentMethod { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
