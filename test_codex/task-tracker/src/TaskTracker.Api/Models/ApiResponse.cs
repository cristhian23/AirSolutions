namespace TaskTracker.Api.Models;

public sealed class ApiError
{
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Details { get; init; } = Array.Empty<string>();
}

public sealed class ApiResponse<T>
{
    public bool Ok { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
}