namespace TaskTracker.Api.Models;

public sealed class TaskUpsertRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Priority { get; init; }
    public string? Status { get; init; }
    public string? DueDate { get; init; }
}

public sealed class StatusPatchRequest
{
    public string? Status { get; init; }
    public bool? Done { get; init; }
}