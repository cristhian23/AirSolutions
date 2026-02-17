using System.Globalization;
using TaskTracker.Api.Models;

namespace TaskTracker.Api.Validation;

public static class TaskValidator
{
    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "low", "medium", "high"
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "todo", "doing", "done"
    };

    public static IReadOnlyList<string> ValidateUpsert(TaskUpsertRequest request)
    {
        var errors = new List<string>();
        var title = (request.Title ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("title is required.");
        }
        else if (title.Length is < 3 or > 80)
        {
            errors.Add("title length must be between 3 and 80 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Length > 500)
        {
            errors.Add("description length must be at most 500 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.Priority) && !AllowedPriorities.Contains(request.Priority))
        {
            errors.Add("priority must be one of: low, medium, high.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !AllowedStatuses.Contains(request.Status))
        {
            errors.Add("status must be one of: todo, doing, done.");
        }

        if (!string.IsNullOrWhiteSpace(request.DueDate) && !IsValidDate(request.DueDate))
        {
            errors.Add("dueDate must be a valid date in YYYY-MM-DD format.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateStatusPatch(StatusPatchRequest request)
    {
        var errors = new List<string>();

        if (request.Done is null && string.IsNullOrWhiteSpace(request.Status))
        {
            errors.Add("Provide either 'status' or 'done'.");
            return errors;
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !AllowedStatuses.Contains(request.Status))
        {
            errors.Add("status must be one of: todo, doing, done.");
        }

        if (request.Done is not null && !string.IsNullOrWhiteSpace(request.Status))
        {
            var normalized = request.Status.Trim().ToLowerInvariant();
            if (request.Done.Value && normalized != "done")
            {
                errors.Add("status must be 'done' when done=true.");
            }

            if (!request.Done.Value && normalized == "done")
            {
                errors.Add("status must not be 'done' when done=false.");
            }
        }

        return errors;
    }

    public static TaskUpsertRequest NormalizeUpsert(TaskUpsertRequest request)
    {
        var normalizedPriority = string.IsNullOrWhiteSpace(request.Priority) ? "medium" : request.Priority.Trim().ToLowerInvariant();
        var normalizedStatus = string.IsNullOrWhiteSpace(request.Status) ? "todo" : request.Status.Trim().ToLowerInvariant();

        return new TaskUpsertRequest
        {
            Title = request.Title?.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Priority = normalizedPriority,
            Status = normalizedStatus,
            DueDate = string.IsNullOrWhiteSpace(request.DueDate) ? null : request.DueDate.Trim()
        };
    }

    public static string ResolveStatus(StatusPatchRequest request)
    {
        if (request.Done is not null)
        {
            return request.Done.Value ? "done" : "todo";
        }

        return request.Status!.Trim().ToLowerInvariant();
    }

    private static bool IsValidDate(string value)
    {
        return DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}