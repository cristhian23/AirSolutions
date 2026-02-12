namespace AirSolutions.Models;

public class Quote
{
    public int Id { get; set; }

    // Asociación con cliente existente
    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
}

