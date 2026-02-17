namespace AirSolutions.Models.Assistant;

public class AssistantInterpretRequest
{
    public string Message { get; set; } = "";
    public AssistantContext? Context { get; set; }
}

public class AssistantContext
{
    public string? CurrentRoute { get; set; }
    public string? SessionId { get; set; }
    public string? Timezone { get; set; }
}

public class AssistantInterpretResponse
{
    public bool Ok { get; set; } = true;
    public string Action { get; set; } = "chat_reply";
    public string Intent { get; set; } = "unknown";
    public decimal Confidence { get; set; } = 0m;
    public string? NextRoute { get; set; }
    public QuotePrefillData Prefill { get; set; } = new();
    public List<string> MissingFields { get; set; } = new();
    public string AssistantMessage { get; set; } = "No identifique una accion para ejecutar.";
}

public class QuotePrefillData
{
    public int? ClientId { get; set; }
    public string? ServiceType { get; set; }
    public string? WorkArea { get; set; }
    public string? MaterialsOrNotes { get; set; }
    public string? ClientName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? ScheduledDate { get; set; }
    public List<QuoteCatalogPrefillLine> CatalogLines { get; set; } = new();
}

public class QuoteCatalogPrefillLine
{
    public int? CatalogItemId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public bool IsTaxable { get; set; }
    public decimal TaxRate { get; set; }
}

public class GeminiSettings
{
    public string Model { get; set; } = "gemini-2.0-flash";
    public string ApiKey { get; set; } = "";
}
