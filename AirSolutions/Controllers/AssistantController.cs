using AirSolutions.Models.Assistant;
using AirSolutions.Data;
using AirSolutions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace AirSolutions.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("assistant")]
public class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistantService;
    private readonly ApplicationDbContext _db;

    public AssistantController(IAssistantService assistantService, ApplicationDbContext db)
    {
        _assistantService = assistantService;
        _db = db;
    }

    [HttpPost("interpret")]
    public async Task<ActionResult<AssistantInterpretResponse>> Interpret(
        [FromBody] AssistantInterpretRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _assistantService.InterpretAsync(request, cancellationToken);
        if (!result.Ok)
        {
            return BadRequest(result);
        }

        await EnrichWithDatabaseMatchesAsync(result, request.Message ?? "", cancellationToken);
        return Ok(result);
    }

    private async Task EnrichWithDatabaseMatchesAsync(AssistantInterpretResponse result, string message, CancellationToken cancellationToken)
    {
        if (!string.Equals(result.Intent, "create_quote", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await TryAttachExistingClientAsync(result, message, cancellationToken);
        await TryAttachCatalogLinesAsync(result, message, cancellationToken);
        UpdateMissingFields(result);
    }

    private async Task TryAttachExistingClientAsync(AssistantInterpretResponse result, string message, CancellationToken cancellationToken)
    {
        var possibleClientName = string.IsNullOrWhiteSpace(result.Prefill.ClientName)
            ? ExtractClientName(message)
            : result.Prefill.ClientName?.Trim();

        if (string.IsNullOrWhiteSpace(possibleClientName))
        {
            return;
        }

        var normalized = possibleClientName.Trim().ToLower();
        var clients = await _db.Clients
            .AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        var exact = clients.FirstOrDefault(c =>
            string.Equals((c.FirstName ?? "").Trim(), possibleClientName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((c.CompanyName ?? "").Trim(), possibleClientName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(((c.FirstName ?? "") + " " + (c.LastName ?? "")).Trim(), possibleClientName, StringComparison.OrdinalIgnoreCase));

        var match = exact ?? clients.FirstOrDefault(c =>
            (c.FirstName != null && c.FirstName.ToLower().Contains(normalized)) ||
            (c.LastName != null && c.LastName.ToLower().Contains(normalized)) ||
            (c.CompanyName != null && c.CompanyName.ToLower().Contains(normalized)));

        if (match == null)
        {
            result.Prefill.ClientName = possibleClientName;
            return;
        }

        result.Prefill.ClientId = match.Id;
        result.Prefill.ClientName = match.ClientType == "Company"
            ? (match.CompanyName ?? possibleClientName)
            : ((match.FirstName + " " + (match.LastName ?? "")).Trim());
        result.Prefill.Phone ??= match.Phone;
        result.Prefill.Address ??= match.Address;
    }

    private async Task TryAttachCatalogLinesAsync(AssistantInterpretResponse result, string message, CancellationToken cancellationToken)
    {
        if (result.Prefill.CatalogLines.Count > 0)
        {
            return;
        }

        var tokens = BuildCatalogTokens(message, result.Prefill.ServiceType, result.Prefill.MaterialsOrNotes);
        if (tokens.Count == 0)
        {
            return;
        }

        var items = await _db.CatalogItems
            .AsNoTracking()
            .Where(i => i.IsActive)
            .ToListAsync(cancellationToken);

        var ranked = items
            .Select(i =>
            {
                var text = ((i.Name ?? "") + " " + (i.Description ?? "")).ToLower();
                var score = tokens.Count(t => text.Contains(t));
                return new { Item = i, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.ItemType == "Service" ? 0 : 1)
            .Take(4)
            .ToList();

        foreach (var entry in ranked)
        {
            result.Prefill.CatalogLines.Add(new QuoteCatalogPrefillLine
            {
                CatalogItemId = entry.Item.Id,
                Name = entry.Item.Name,
                Description = entry.Item.Description,
                Quantity = 1m,
                UnitPrice = entry.Item.BasePrice ?? 0m,
                IsTaxable = entry.Item.IsTaxable,
                TaxRate = entry.Item.IsTaxable ? 18m : 0m
            });
        }
    }

    private static List<string> BuildCatalogTokens(string message, string? serviceType, string? materialsNotes)
    {
        var source = (message + " " + (serviceType ?? "") + " " + (materialsNotes ?? "")).ToLower();
        var rawTokens = Regex.Split(source, @"[^a-z0-9áéíóúñ]+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct()
            .ToList();

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "quiero","hacer","una","un","de","del","con","para","cotización","cotizar",
            "instalacion","servicio","cliente","segundo","piso","2do","primer","1er"
        };

        var tokens = rawTokens
            .Where(t => t.Length >= 4 && !stopWords.Contains(t))
            .ToList();

        if (!string.IsNullOrWhiteSpace(serviceType))
        {
            tokens.Add(serviceType.Trim().ToLower());
        }

        return tokens.Distinct().ToList();
    }

    private static string? ExtractClientName(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = Regex.Match(message, @"cliente\s+([a-zA-ZáéíóúÁÉÍÓÚñÑ0-9]+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.Trim();
    }

    private static void UpdateMissingFields(AssistantInterpretResponse result)
    {
        result.MissingFields = result.MissingFields
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (result.Prefill.ClientId.HasValue)
        {
            result.MissingFields.RemoveAll(f => string.Equals(f, "clientName", StringComparison.OrdinalIgnoreCase));
        }

        if (result.Prefill.CatalogLines.Count > 0)
        {
            result.MissingFields.RemoveAll(f => string.Equals(f, "serviceType", StringComparison.OrdinalIgnoreCase));
        }
    }
}

