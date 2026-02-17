using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskTracker.Api.Tests;

public sealed class TasksApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TasksApiTests(WebApplicationFactory<Program> factory)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"task-tracker-tests-{Guid.NewGuid():N}.db");
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TASKTRACKER_DB_PATH", dbPath);
        });
    }

    [Fact]
    public async Task Post_Creates_Task()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Write API tests",
            description = "Ensure endpoints are covered",
            priority = "high",
            status = "todo",
            dueDate = "2026-03-01"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<Dictionary<string, object>>>();
        Assert.NotNull(payload);
        Assert.True(payload!.Ok);
    }

    [Fact]
    public async Task Post_Fails_When_Title_Is_Invalid()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/tasks", new { title = "ab" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        Assert.NotNull(payload);
        Assert.False(payload!.Ok);
        Assert.Contains("title length", payload.Error?.Details?.FirstOrDefault() ?? string.Empty);
    }

    [Fact]
    public async Task Get_List_With_Status_Filter_Works()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/tasks", new { title = "Task one", status = "todo" });
        await client.PostAsJsonAsync("/api/tasks", new { title = "Task two", status = "done" });

        var response = await client.GetAsync("/api/tasks?status=done");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TaskDto>>>();
        Assert.NotNull(payload);
        Assert.True(payload!.Ok);
        Assert.Single(payload.Data!);
        Assert.Equal("done", payload.Data![0].Status);
    }

    [Fact]
    public async Task Patch_Status_Changes_Task_Status()
    {
        var client = _factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/tasks", new { title = "Patch me" });
        var created = await create.Content.ReadFromJsonAsync<ApiEnvelope<TaskDto>>();

        var response = await client.PatchAsJsonAsync($"/api/tasks/{created!.Data!.Id}/status", new { done = true });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<TaskDto>>();
        Assert.NotNull(payload);
        Assert.Equal("done", payload!.Data!.Status);
    }

    [Fact]
    public async Task Delete_Removes_Task()
    {
        var client = _factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/tasks", new { title = "Delete me" });
        var created = await create.Content.ReadFromJsonAsync<ApiEnvelope<TaskDto>>();

        var deleteResponse = await client.DeleteAsync($"/api/tasks/{created!.Data!.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync($"/api/tasks/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    private sealed class ApiEnvelope<T>
    {
        public bool Ok { get; init; }
        public T? Data { get; init; }
        public ApiErrorEnvelope? Error { get; init; }
    }

    private sealed class ApiErrorEnvelope
    {
        public string Message { get; init; } = string.Empty;
        public List<string> Details { get; init; } = [];
    }

    private sealed class TaskDto
    {
        public int Id { get; init; }
        public string Status { get; init; } = string.Empty;
    }
}
