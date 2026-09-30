using Taskcli;
using Xunit;

namespace Taskcli.Tests;

public class ProgramTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storagePath;

    public ProgramTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _storagePath = Path.Combine(_tempDirectory, "tasks.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Run_AddWithDescription_PrintsIdAndDescriptionAndReturnsZero()
    {
        var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);
        try
        {
            int exitCode = Program.Run(new[] { "add", "Buy", "milk" }, _storagePath);

            Assert.Equal(0, exitCode);
            Assert.Equal("1: Buy milk", stdout.ToString().Trim());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Run_ListWithTasks_PrintsTasksInIdOrderWithStatusAndReturnsZero()
    {
        var repository = new TaskRepository(_storagePath);
        repository.Save(new TaskStore
        {
            NextId = 3,
            Tasks =
            {
                new TaskItem { Id = 2, Description = "Second", Status = TaskState.Completed },
                new TaskItem { Id = 1, Description = "First", Status = TaskState.Pending }
            }
        });
        string storedJsonBeforeList = File.ReadAllText(_storagePath);

        var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);
        try
        {
            int exitCode = Program.Run(new[] { "list" }, _storagePath);

            Assert.Equal(0, exitCode);
            Assert.Equal(
                $"1: First [pending]{Environment.NewLine}2: Second [completed]",
                stdout.ToString().TrimEnd());
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(storedJsonBeforeList, File.ReadAllText(_storagePath));
    }

    [Fact]
    public void Run_ListWithEmptyStorage_PrintsEmptyStateAndReturnsZeroWithoutCreatingFile()
    {
        var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);
        try
        {
            int exitCode = Program.Run(new[] { "list" }, _storagePath);

            Assert.Equal(0, exitCode);
            Assert.Equal("No tasks found.", stdout.ToString().Trim());
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.False(File.Exists(_storagePath));
        Assert.False(Directory.Exists(_tempDirectory));
    }

    [Fact]
    public void Run_ListWithCorruptStorage_PrintsErrorAndReturnsOneWithoutChangingFile()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_storagePath, "{ not valid json ");

        var stderr = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(stderr);
        try
        {
            int exitCode = Program.Run(new[] { "list" }, _storagePath);

            Assert.Equal(1, exitCode);
            Assert.Contains("Error:", stderr.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal("{ not valid json ", File.ReadAllText(_storagePath));
    }

    [Fact]
    public void Run_AddWithEmptyDescription_PrintsErrorAndReturnsOne()
    {
        var stderr = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(stderr);
        try
        {
            int exitCode = Program.Run(new[] { "add", "   " }, _storagePath);

            Assert.Equal(1, exitCode);
            Assert.Contains("Error:", stderr.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Run_WithUnrecognizedVerb_PrintsUsageErrorAndReturnsOne()
    {
        var stderr = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(stderr);
        try
        {
            int exitCode = Program.Run(new[] { "bogus" }, _storagePath);

            Assert.Equal(1, exitCode);
            Assert.Contains("Unrecognized command", stderr.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Run_WithNoArguments_PrintsUsageErrorAndReturnsOne()
    {
        var stderr = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(stderr);
        try
        {
            int exitCode = Program.Run(Array.Empty<string>(), _storagePath);

            Assert.Equal(1, exitCode);
            Assert.Contains("Usage:", stderr.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Run_AddWithCorruptStorageFile_PrintsErrorAndReturnsOneWithoutCrashing()
    {
        // End-to-end check that TaskStorageException thrown by TaskRepository.Load
        // is actually caught and surfaced by Program.Run's error handling (issue #7:
        // "fails with a clear error rather than silently discarding data"). Prior
        // coverage only exercised TaskRepository.Load in isolation, not this path.
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_storagePath, "{ not valid json ");

        var stderr = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(stderr);
        try
        {
            int exitCode = Program.Run(new[] { "add", "Buy milk" }, _storagePath);

            Assert.Equal(1, exitCode);
            Assert.Contains("Error:", stderr.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }

        // The corrupt file must not have been silently overwritten/discarded.
        Assert.Equal("{ not valid json ", File.ReadAllText(_storagePath));
    }

    [Fact]
    public void Run_AddInvokedTwiceAsSeparateCalls_PersistsAndIncrementsIdAcrossRuns()
    {
        // Simulates two separate process invocations (issue #7: tasks survive
        // process exit) by using two independent Program.Run calls against the
        // same storage path rather than a single long-lived service instance.
        var stdout1 = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout1);
        int firstExitCode;
        try
        {
            firstExitCode = Program.Run(new[] { "add", "First", "task" }, _storagePath);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var stdout2 = new StringWriter();
        Console.SetOut(stdout2);
        int secondExitCode;
        try
        {
            secondExitCode = Program.Run(new[] { "add", "Second", "task" }, _storagePath);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(0, firstExitCode);
        Assert.Equal(0, secondExitCode);
        Assert.Equal("1: First task", stdout1.ToString().Trim());
        Assert.Equal("2: Second task", stdout2.ToString().Trim());

        string json = File.ReadAllText(_storagePath);
        Assert.Contains("\"id\": 1", json);
        Assert.Contains("\"id\": 2", json);
        Assert.Contains("\"nextId\": 3", json);
    }
}
