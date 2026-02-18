using AirSolutions.Models.Assistant;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AirSolutions.Services;

public class GeminiAssistantService : IAssistantService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _geminiSettings;
    private readonly ILogger<GeminiAssistantService> _logger;

    public GeminiAssistantService(
        HttpClient httpClient,
        IOptions<GeminiSettings> geminiOptions,
        ILogger<GeminiAssistantService> logger)
    {
        _httpClient = httpClient;
        _geminiSettings = geminiOptions.Value;
        _logger = logger;

        var envApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            _geminiSettings.ApiKey = envApiKey.Trim();
        }
    }

    public async Task<AssistantInterpretResponse> InterpretAsync(AssistantInterpretRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new AssistantInterpretResponse
            {
                Ok = false,
                AssistantMessage = "Debes escribir un mensaje para continuar."
            };
        }

        if (string.IsNullOrWhiteSpace(_geminiSettings.ApiKey))
        {
            return BuildHeuristicResponse(request.Message);
        }

        try
        {
            var providerResponse = await CallGeminiAsync(request.Message, cancellationToken);
            if (providerResponse != null)
            {
                return providerResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini provider error. Falling back to local heuristic parser.");
        }

        return BuildHeuristicResponse(request.Message);
    }

    private async Task<AssistantInterpretResponse?> CallGeminiAsync(string message, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(message);
        var model = string.IsNullOrWhiteSpace(_geminiSettings.Model) ? "gemini-2.0-flash" : _geminiSettings.Model.Trim();
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_geminiSettings.ApiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json"
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Gemini non-success status {Status}. Body: {Body}", response.StatusCode, error);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        var text = ExtractGeminiText(doc.RootElement);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        using var parsed = JsonDocument.Parse(text);
        return NormalizeResponse(parsed.RootElement);
    }

    private static string BuildPrompt(string message)
    {
        return
            "Eres un clasificador de intencion para AirSolutions.\n" +
            "Analiza el mensaje y responde SOLO JSON valido con este esquema:\n" +
            "{\n" +
            "  \"action\": \"open_quote_create|chat_reply\",\n" +
            "  \"intent\": \"create_quote|unknown\",\n" +
            "  \"confidence\": 0.0,\n" +
            "  \"nextRoute\": \"/quotes/create|null\",\n" +
            "  \"prefill\": {\n" +
            "    \"clientId\": number|null,\n" +
            "    \"serviceType\": \"string|null\",\n" +
            "    \"workArea\": \"string|null\",\n" +
            "    \"materialsOrNotes\": \"string|null\",\n" +
            "    \"clientName\": \"string|null\",\n" +
            "    \"phone\": \"string|null\",\n" +
            "    \"address\": \"string|null\",\n" +
            "    \"quantity\": number|null,\n" +
            "    \"unit\": \"string|null\",\n" +
            "    \"unitPrice\": number|null,\n" +
            "    \"scheduledDate\": \"string|null\",\n" +
            "    \"catalogLines\": [\n" +
            "      {\n" +
            "        \"catalogItemId\": number|null,\n" +
            "        \"name\": \"string\",\n" +
            "        \"description\": \"string|null\",\n" +
            "        \"quantity\": number,\n" +
            "        \"unitPrice\": number,\n" +
            "        \"isTaxable\": true,\n" +
            "        \"taxRate\": number\n" +
            "      }\n" +
            "    ]\n" +
            "  },\n" +
            "  \"missingFields\": [\"array de strings\"],\n" +
            "  \"assistantMessage\": \"string\"\n" +
            "}\n\n" +
            "Reglas:\n" +
            "- Si piden crear cotización => action=open_quote_create, intent=create_quote, nextRoute=/quotes/create.\n" +
            "- No inventes datos numericos si no vienen en el texto.\n" +
            "- Campos no detectados deben ir en null.\n" +
            "- missingFields debe listar datos útiles para completar una cotización.\n" +
            "- Responde en espanol en assistantMessage.\n\n" +
            "Mensaje del usuario:\n" + message;
    }

    private static string? ExtractGeminiText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content))
            {
                continue;
            }

            if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    private static AssistantInterpretResponse NormalizeResponse(JsonElement root)
    {
        var action = root.TryGetProperty("action", out var actionEl) ? actionEl.GetString() : "chat_reply";
        var intent = root.TryGetProperty("intent", out var intentEl) ? intentEl.GetString() : "unknown";
        var confidence = root.TryGetProperty("confidence", out var confEl) && confEl.TryGetDecimal(out var confVal) ? confVal : 0m;
        var nextRoute = root.TryGetProperty("nextRoute", out var routeEl) ? routeEl.GetString() : null;
        var assistantMessage = root.TryGetProperty("assistantMessage", out var msgEl) ? msgEl.GetString() : null;

        var prefill = new QuotePrefillData();
        if (root.TryGetProperty("prefill", out var prefillEl) && prefillEl.ValueKind == JsonValueKind.Object)
        {
            prefill.ClientId = GetInt(prefillEl, "clientId");
            prefill.ServiceType = GetString(prefillEl, "serviceType");
            prefill.WorkArea = GetString(prefillEl, "workArea");
            prefill.MaterialsOrNotes = GetString(prefillEl, "materialsOrNotes");
            prefill.ClientName = GetString(prefillEl, "clientName");
            prefill.Phone = GetString(prefillEl, "phone");
            prefill.Address = GetString(prefillEl, "address");
            prefill.Unit = GetString(prefillEl, "unit");
            prefill.ScheduledDate = GetString(prefillEl, "scheduledDate");
            prefill.Quantity = GetDecimal(prefillEl, "quantity");
            prefill.UnitPrice = GetDecimal(prefillEl, "unitPrice");
            if (prefillEl.TryGetProperty("catalogLines", out var linesEl) && linesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var lineEl in linesEl.EnumerateArray())
                {
                    if (lineEl.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = GetString(lineEl, "name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    prefill.CatalogLines.Add(new QuoteCatalogPrefillLine
                    {
                        CatalogItemId = GetInt(lineEl, "catalogItemId"),
                        Name = name.Trim(),
                        Description = GetString(lineEl, "description"),
                        Quantity = GetDecimal(lineEl, "quantity") ?? 1m,
                        UnitPrice = GetDecimal(lineEl, "unitPrice") ?? 0m,
                        IsTaxable = GetBool(lineEl, "isTaxable") ?? false,
                        TaxRate = GetDecimal(lineEl, "taxRate") ?? 0m
                    });
                }
            }
        }

        var missingFields = new List<string>();
        if (root.TryGetProperty("missingFields", out var missingEl) && missingEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in missingEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        missingFields.Add(value);
                    }
                }
            }
        }

        return new AssistantInterpretResponse
        {
            Ok = true,
            Action = string.IsNullOrWhiteSpace(action) ? "chat_reply" : action,
            Intent = string.IsNullOrWhiteSpace(intent) ? "unknown" : intent,
            Confidence = confidence,
            NextRoute = nextRoute,
            Prefill = prefill,
            MissingFields = missingFields,
            AssistantMessage = string.IsNullOrWhiteSpace(assistantMessage)
                ? "Procesamos tu solicitud."
                : assistantMessage
        };
    }

    private static AssistantInterpretResponse BuildHeuristicResponse(string message)
    {
        var lower = message.Trim().ToLowerInvariant();
        if (!lower.Contains("cotiza"))
        {
            return new AssistantInterpretResponse
            {
                Ok = true,
                Action = "chat_reply",
                Intent = "unknown",
                Confidence = 0.45m,
                AssistantMessage = "Puedo ayudarte a crear una cotización. Indica servicio, area, cliente y cantidades."
            };
        }

        var prefill = new QuotePrefillData
        {
            ServiceType = ExtractServiceType(lower),
            WorkArea = ExtractWorkArea(lower),
            MaterialsOrNotes = ExtractNotes(lower)
        };

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(prefill.ClientName)) missing.Add("clientName");
        if (!prefill.Quantity.HasValue) missing.Add("quantity");
        if (!prefill.UnitPrice.HasValue) missing.Add("unitPrice");
        if (string.IsNullOrWhiteSpace(prefill.Phone)) missing.Add("phone");
        if (string.IsNullOrWhiteSpace(prefill.Address)) missing.Add("address");

        return new AssistantInterpretResponse
        {
            Ok = true,
            Action = "open_quote_create",
            Intent = "create_quote",
            Confidence = 0.80m,
            NextRoute = "/quotes/create.html",
            Prefill = prefill,
            MissingFields = missing,
            AssistantMessage = "Abriré el módulo de cotizaciónes y completaré los campos detectados."
        };
    }

    private static string? ExtractServiceType(string lower)
    {
        if (lower.Contains("instal")) return "instalacion";
        if (lower.Contains("mantenimiento")) return "mantenimiento";
        if (lower.Contains("repar")) return "reparacion";
        return null;
    }

    private static string? ExtractWorkArea(string lower)
    {
        if (Regex.IsMatch(lower, @"\b2do piso\b|\bsegundo piso\b")) return "2do piso";
        if (Regex.IsMatch(lower, @"\b1er piso\b|\bprimer piso\b")) return "1er piso";
        return null;
    }

    private static string? ExtractNotes(string lower)
    {
        var match = Regex.Match(lower, @"\bcon\s+(.+)$");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? GetString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static decimal? GetDecimal(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? GetInt(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? GetBool(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        return null;
    }
}

