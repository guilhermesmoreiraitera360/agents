namespace Taskcli;

/// <summary>
/// Business logic for managing tasks. Contains no console or direct file
/// I/O; persistence is delegated to a <see cref="TaskRepository"/>.
/// </summary>
public class TaskService
{
    private readonly TaskRepository _repository;

    public TaskService(TaskRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Returns all tasks ordered by ascending ID without modifying storage.
    /// </summary>
    public IReadOnlyList<TaskItem> List()
    {
        return _repository.Load().Tasks.OrderBy(task => task.Id).ToArray();
    }

    /// <summary>
    /// Creates a new pending task with the given description, persists it,
    /// and returns the created task.
    /// </summary>
    /// <exception cref="TaskValidationException">
    /// Thrown when <paramref name="description"/> is empty or whitespace-only.
    /// </exception>
    public TaskItem Add(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new TaskValidationException("Task description must not be empty.");
        }

        TaskStore store = _repository.Load();

        var task = new TaskItem
        {
            Id = store.NextId,
            Description = description.Trim(),
            Status = TaskState.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        store.Tasks.Add(task);
        store.NextId++;

        _repository.Save(store);

        return task;
    }
}
