namespace TaskTracker.Api.Models;

public sealed class TaskItem
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Priority { get; init; } = "medium";
    public string Status { get; init; } = "todo";
    public string? DueDate { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}