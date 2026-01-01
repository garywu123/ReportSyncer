// ============================================================================
// File: FileSystemRunLogWriterTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Unit tests for FileSystemRunLogWriter file operations.
// ============================================================================

using FluentAssertions;
using ReportSyncer.Core.Observability.RunLog;
using ReportSyncer.Core.Schema;
using System.Text.Json;
using Xunit;

namespace ReportSyncer.Core.Tests.Observability.RunLog;

public sealed class FileSystemRunLogWriterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly RunLogLayoutSpec _layout;
    private readonly FileSystemRunLogWriter _writer;

    public FileSystemRunLogWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"RunLogTests_{Guid.NewGuid():N}");
        _layout = new RunLogLayoutSpec(_tempDirectory);
        _writer = new FileSystemRunLogWriter(_layout);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AppendJobAsync_WithSingleEntry_CreatesFileWithOneLine()
    {
        // Arrange
        var entry = CreateJobEntry("test-job", "Started");

        // Act
        await _writer.AppendJobAsync(entry);

        // Assert
        var files = Directory.GetFiles(_tempDirectory, "job.jsonl", SearchOption.AllDirectories);
        files.Should().HaveCount(1);

        var lines = await File.ReadAllLinesAsync(files[0]);
        lines.Should().HaveCount(1);
    }

    [Fact]
    public async Task AppendJobAsync_WithTwoEntries_CreatesFileWithTwoLines()
    {
        // Arrange
        var entry1 = CreateJobEntry("test-job", "Started");
        var entry2 = CreateJobEntry("test-job", "Completed");

        // Act
        await _writer.AppendJobAsync(entry1);
        await _writer.AppendJobAsync(entry2);

        // Assert
        var files = Directory.GetFiles(_tempDirectory, "job.jsonl", SearchOption.AllDirectories);
        files.Should().HaveCount(1);

        var lines = await File.ReadAllLinesAsync(files[0]);
        lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task AppendTableAsync_WithEntry_CreatesTablesSubdirectory()
    {
        // Arrange
        var entry = CreateTableEntry("test-job", "dbo.Users", "Insert");

        // Act
        await _writer.AppendTableAsync(entry);

        // Assert
        var tablesDir = Directory.GetDirectories(_tempDirectory, "tables", SearchOption.AllDirectories);
        tablesDir.Should().HaveCount(1);

        var files = Directory.GetFiles(tablesDir[0], "*.jsonl");
        files.Should().HaveCount(1);
    }

    [Fact]
    public async Task AppendTableAsync_WithMultipleTables_CreatesMultipleFiles()
    {
        // Arrange
        var entry1 = CreateTableEntry("test-job", "dbo.Users", "Insert");
        var entry2 = CreateTableEntry("test-job", "dbo.Orders", "Insert");

        // Act
        await _writer.AppendTableAsync(entry1);
        await _writer.AppendTableAsync(entry2);

        // Assert
        var tablesDir = Directory.GetDirectories(_tempDirectory, "tables", SearchOption.AllDirectories);
        tablesDir.Should().HaveCount(1);

        var files = Directory.GetFiles(tablesDir[0], "*.jsonl");
        files.Should().HaveCount(2);
    }

    [Fact]
    public async Task AppendTableAsync_WithNullTable_ThrowsInvalidOperationException()
    {
        // Arrange
        var entry = new RunLogEntry
        {
            UtcTimestamp = DateTimeOffset.UtcNow.ToString("O"),
            Level = "Info",
            JobId = "test-job",
            EventKind = "Started",
            Phase = "Insert",
            Table = null
        };

        // Act
        var act = async () => await _writer.AppendTableAsync(entry);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Table*");
    }

    [Fact]
    public async Task AppendJobAsync_CreatesValidJsonLines()
    {
        // Arrange
        var entry = CreateJobEntry("test-job", "Completed");

        // Act
        await _writer.AppendJobAsync(entry);

        // Assert
        var files = Directory.GetFiles(_tempDirectory, "job.jsonl", SearchOption.AllDirectories);
        var content = await File.ReadAllTextAsync(files[0]);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        var deserializedEntry = JsonSerializer.Deserialize<RunLogEntry>(lines[0], new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        deserializedEntry.Should().NotBeNull();
        deserializedEntry!.JobId.Should().Be("test-job");
        deserializedEntry.EventKind.Should().Be("Completed");
    }

    [Fact]
    public async Task AppendJobAsync_WithSameJobId_UsesConsistentRunId()
    {
        // Arrange
        var entry1 = CreateJobEntry("test-job", "Started");
        var entry2 = CreateJobEntry("test-job", "Completed");

        // Act
        await _writer.AppendJobAsync(entry1);
        await _writer.AppendJobAsync(entry2);

        // Assert - should be only one run directory for the job
        var jobDirs = Directory.GetDirectories(_tempDirectory, "*", SearchOption.TopDirectoryOnly);
        jobDirs.Should().HaveCount(1);

        var runDirs = Directory.GetDirectories(jobDirs[0], "*", SearchOption.TopDirectoryOnly);
        runDirs.Should().HaveCount(1);
    }

    [Fact]
    public async Task AppendTableAsync_WithSameTable_AppendsToSameFile()
    {
        // Arrange
        var entry1 = CreateTableEntry("test-job", "dbo.Users", "Delete");
        var entry2 = CreateTableEntry("test-job", "dbo.Users", "Insert");

        // Act
        await _writer.AppendTableAsync(entry1);
        await _writer.AppendTableAsync(entry2);

        // Assert
        var tablesDir = Directory.GetDirectories(_tempDirectory, "tables", SearchOption.AllDirectories);
        var files = Directory.GetFiles(tablesDir[0], "*.jsonl");
        files.Should().HaveCount(1);

        var lines = await File.ReadAllLinesAsync(files[0]);
        lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task AppendJobAsync_ConcurrentWrites_AllEntriesWritten()
    {
        // Arrange
        var entries = Enumerable.Range(1, 10)
            .Select(i => CreateJobEntry("test-job", $"Event{i}"))
            .ToList();

        // Act - write concurrently
        var tasks = entries.Select(e => _writer.AppendJobAsync(e));
        await Task.WhenAll(tasks);

        // Assert
        var files = Directory.GetFiles(_tempDirectory, "job.jsonl", SearchOption.AllDirectories);
        var lines = await File.ReadAllLinesAsync(files[0]);
        lines.Should().HaveCount(10);
    }

    private static RunLogEntry CreateJobEntry(string jobId, string eventKind)
    {
        return new RunLogEntry
        {
            UtcTimestamp = DateTimeOffset.UtcNow.ToString("O"),
            Level = "Info",
            JobId = jobId,
            EventKind = eventKind,
            Phase = "Execution"
        };
    }

    private static RunLogEntry CreateTableEntry(string jobId, string table, string phase)
    {
        return new RunLogEntry
        {
            UtcTimestamp = DateTimeOffset.UtcNow.ToString("O"),
            Level = "Info",
            JobId = jobId,
            EventKind = "Completed",
            Phase = phase,
            Table = table
        };
    }
}
