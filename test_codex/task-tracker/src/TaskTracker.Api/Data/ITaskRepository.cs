using TaskTracker.Api.Models;

namespace TaskTracker.Api.Data;

public sealed class TaskListFilters
{
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? Search { get; init; }
    public string Sort { get; init; } = "createdAt";
    public string Order { get; init; } = "desc";
}

public interface ITaskRepository
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    Task<TaskItem> CreateTaskAsync(TaskUpsertRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> ListTasksAsync(TaskListFilters filters, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetTaskByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TaskItem?> UpdateTaskAsync(int id, TaskUpsertRequest request, CancellationToken cancellationToken = default);
    Task<TaskItem?> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default);
    Task<bool> DeleteTaskAsync(int id, CancellationToken cancellationToken = default);
}