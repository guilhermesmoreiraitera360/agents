using Taskcli;
using Xunit;

namespace Taskcli.Tests;

public class TaskServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly TaskService _service;

    public TaskServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string storagePath = Path.Combine(_tempDirectory, "tasks.json");
        _service = new TaskService(new TaskRepository(storagePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Add_WithValidDescription_CreatesPendingTaskWithExpectedFields()
    {
        TaskItem task = _service.Add("Buy milk");

        Assert.Equal(1, task.Id);
        Assert.Equal("Buy milk", task.Description);
        Assert.Equal(TaskState.Pending, task.Status);
        Assert.True((DateTime.UtcNow - task.CreatedAtUtc) < TimeSpan.FromMinutes(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Add_WithEmptyOrWhitespaceDescription_ThrowsValidationException(string? description)
    {
        Assert.Throws<TaskValidationException>(() => _service.Add(description!));
    }

    [Fact]
    public void Add_CalledTwice_ReturnsDistinctIncrementingIds()
    {
        TaskItem first = _service.Add("First task");
        TaskItem second = _service.Add("Second task");

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void List_ReturnsTasksInAscendingIdOrder()
    {
        string storagePath = Path.Combine(_tempDirectory, "tasks.json");
        var repository = new TaskRepository(storagePath);
        repository.Save(new TaskStore
        {
            NextId = 3,
            Tasks =
            {
                new TaskItem { Id = 2, Description = "Second", Status = TaskState.Completed },
                new TaskItem { Id = 1, Description = "First", Status = TaskState.Pending }
            }
        });

        IReadOnlyList<TaskItem> tasks = _service.List();

        Assert.Equal(new[] { 1, 2 }, tasks.Select(task => task.Id));
    }

    [Fact]
    public void List_WhenStorageIsMissing_ReturnsEmptyWithoutCreatingStorage()
    {
        IReadOnlyList<TaskItem> tasks = _service.List();

        Assert.Empty(tasks);
        Assert.False(Directory.Exists(_tempDirectory));
    }
}
