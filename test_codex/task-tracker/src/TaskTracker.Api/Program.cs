using TaskTracker.Api.Data;
using TaskTracker.Api.Models;
using TaskTracker.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

var dbPath = builder.Configuration["TASKTRACKER_DB_PATH"]
    ?? builder.Configuration["Database:Path"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "app.db");

builder.Services.AddSingleton<ITaskRepository>(_ => new SqliteTaskRepository(dbPath));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    var started = DateTime.UtcNow;
    await next();
    var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
    app.Logger.LogInformation("{Method} {Path} => {StatusCode} in {Elapsed:0.00} ms",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        elapsed);
});

using (var scope = app.Services.CreateScope())
{
    var repo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
    await repo.EnsureInitializedAsync();
}

app.MapGet("/api/tasks", async (
    string? status,
    string? priority,
    string? search,
    string? sort,
    string? order,
    ITaskRepository repo,
    CancellationToken ct) =>
{
    var filters = new TaskListFilters
    {
        Status = status,
        Priority = priority,
        Search = search,
        Sort = sort ?? "createdAt",
        Order = order ?? "desc"
    };

    var tasks = await repo.ListTasksAsync(filters, ct);
    return Ok(tasks);
});

app.MapGet("/api/tasks/{id:int}", async (int id, ITaskRepository repo, CancellationToken ct) =>
{
    var task = await repo.GetTaskByIdAsync(id, ct);
    return task is null ? NotFound($"Task {id} not found.") : Ok(task);
});

app.MapPost("/api/tasks", async (TaskUpsertRequest request, ITaskRepository repo, CancellationToken ct) =>
{
    var errors = TaskValidator.ValidateUpsert(request);
    if (errors.Count > 0)
    {
        return ValidationError(errors);
    }

    var normalized = TaskValidator.NormalizeUpsert(request);
    var created = await repo.CreateTaskAsync(normalized, ct);
    return Results.Json(new ApiResponse<TaskItem> { Ok = true, Data = created }, statusCode: StatusCodes.Status201Created);
});

app.MapPut("/api/tasks/{id:int}", async (int id, TaskUpsertRequest request, ITaskRepository repo, CancellationToken ct) =>
{
    var errors = TaskValidator.ValidateUpsert(request);
    if (errors.Count > 0)
    {
        return ValidationError(errors);
    }

    var normalized = TaskValidator.NormalizeUpsert(request);
    var updated = await repo.UpdateTaskAsync(id, normalized, ct);
    return updated is null ? NotFound($"Task {id} not found.") : Ok(updated);
});

app.MapPatch("/api/tasks/{id:int}/status", async (int id, StatusPatchRequest request, ITaskRepository repo, CancellationToken ct) =>
{
    var errors = TaskValidator.ValidateStatusPatch(request);
    if (errors.Count > 0)
    {
        return ValidationError(errors);
    }

    var status = TaskValidator.ResolveStatus(request);
    var updated = await repo.UpdateStatusAsync(id, status, ct);
    return updated is null ? NotFound($"Task {id} not found.") : Ok(updated);
});

app.MapDelete("/api/tasks/{id:int}", async (int id, ITaskRepository repo, CancellationToken ct) =>
{
    var deleted = await repo.DeleteTaskAsync(id, ct);
    return deleted ? Ok(new { id }) : NotFound($"Task {id} not found.");
});

app.MapFallbackToFile("index.html");

app.Run();

static IResult Ok<T>(T data)
{
    return Results.Json(new ApiResponse<T> { Ok = true, Data = data });
}

static IResult ValidationError(IReadOnlyList<string> details)
{
    return Results.Json(new ApiResponse<object>
    {
        Ok = false,
        Error = new ApiError
        {
            Message = "Validation failed.",
            Details = details
        }
    }, statusCode: StatusCodes.Status400BadRequest);
}

static IResult NotFound(string message)
{
    return Results.Json(new ApiResponse<object>
    {
        Ok = false,
        Error = new ApiError
        {
            Message = message
        }
    }, statusCode: StatusCodes.Status404NotFound);
}

public partial class Program;