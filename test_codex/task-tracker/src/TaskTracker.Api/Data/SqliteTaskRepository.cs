using Microsoft.Data.Sqlite;
using TaskTracker.Api.Models;

namespace TaskTracker.Api.Data;

public sealed class SqliteTaskRepository(string dbPath) : ITaskRepository
{
    private readonly string _connectionString = $"Data Source={dbPath}";

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    description TEXT NULL,
    priority TEXT NOT NULL DEFAULT 'medium',
    status TEXT NOT NULL DEFAULT 'todo',
    dueDate TEXT NULL,
    createdAt TEXT NOT NULL,
    updatedAt TEXT NOT NULL
);
";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TaskItem> CreateTaskAsync(TaskUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO tasks (title, description, priority, status, dueDate, createdAt, updatedAt)
VALUES ($title, $description, $priority, $status, $dueDate, $createdAt, $updatedAt);
SELECT last_insert_rowid();
";
        command.Parameters.AddWithValue("$title", request.Title!);
        command.Parameters.AddWithValue("$description", (object?)request.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$priority", request.Priority!);
        command.Parameters.AddWithValue("$status", request.Status!);
        command.Parameters.AddWithValue("$dueDate", (object?)request.DueDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return (await GetTaskByIdAsync(id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<TaskItem>> ListTasksAsync(TaskListFilters filters, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        var clauses = new List<string>();

        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            clauses.Add("status = $status");
            command.Parameters.AddWithValue("$status", filters.Status.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(filters.Priority))
        {
            clauses.Add("priority = $priority");
            command.Parameters.AddWithValue("$priority", filters.Priority.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            clauses.Add("(title LIKE $search OR COALESCE(description, '') LIKE $search)");
            command.Parameters.AddWithValue("$search", $"%{filters.Search.Trim()}%");
        }

        var where = clauses.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", clauses)}";
        var orderColumn = ResolveSortColumn(filters.Sort);
        var orderDirection = ResolveOrderDirection(filters.Order);

        command.CommandText = $@"
SELECT id, title, description, priority, status, dueDate, createdAt, updatedAt
FROM tasks
{where}
ORDER BY {orderColumn} {orderDirection};
";

        var results = new List<TaskItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapTask(reader));
        }

        return results;
    }

    public async Task<TaskItem?> GetTaskByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, title, description, priority, status, dueDate, createdAt, updatedAt
FROM tasks
WHERE id = $id;
";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapTask(reader);
        }

        return null;
    }

    public async Task<TaskItem?> UpdateTaskAsync(int id, TaskUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var current = await GetTaskByIdAsync(id, cancellationToken);
        if (current is null)
        {
            return null;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE tasks
SET title = $title,
    description = $description,
    priority = $priority,
    status = $status,
    dueDate = $dueDate,
    updatedAt = $updatedAt
WHERE id = $id;
";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", request.Title!);
        command.Parameters.AddWithValue("$description", (object?)request.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$priority", request.Priority!);
        command.Parameters.AddWithValue("$status", request.Status!);
        command.Parameters.AddWithValue("$dueDate", (object?)request.DueDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetTaskByIdAsync(id, cancellationToken);
    }

    public async Task<TaskItem?> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default)
    {
        var current = await GetTaskByIdAsync(id, cancellationToken);
        if (current is null)
        {
            return null;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE tasks
SET status = $status,
    updatedAt = $updatedAt
WHERE id = $id;
";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetTaskByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteTaskAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tasks WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private static TaskItem MapTask(SqliteDataReader reader)
    {
        return new TaskItem
        {
            Id = reader.GetInt32(0),
            Title = reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            Priority = reader.GetString(3),
            Status = reader.GetString(4),
            DueDate = reader.IsDBNull(5) ? null : reader.GetString(5),
            CreatedAt = reader.GetString(6),
            UpdatedAt = reader.GetString(7)
        };
    }

    private static string ResolveSortColumn(string? sort)
    {
        return (sort ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "duedate" => "COALESCE(dueDate, '9999-12-31')",
            "priority" => "CASE priority WHEN 'low' THEN 1 WHEN 'medium' THEN 2 WHEN 'high' THEN 3 ELSE 4 END",
            _ => "createdAt"
        };
    }

    private static string ResolveOrderDirection(string? order)
    {
        return string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
    }
}