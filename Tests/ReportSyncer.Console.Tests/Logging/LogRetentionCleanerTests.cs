// ============================================================================
// File: LogRetentionCleanerTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Tests for LogRetentionCleaner.
// ============================================================================

using ReportSyncer.Console.Logging;
using Xunit;

namespace ReportSyncer.Console.Tests.Logging;

public sealed class LogRetentionCleanerTests
{
    [Fact]
    public void BestEffortCleanup_WithNonExistentDirectory_ReturnsSuccess()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var (success, deletedCount, errorMessage) = LogRetentionCleaner.BestEffortCleanup(
            nonExistentDir, "test", 20);

        // Assert
        Assert.True(success);
        Assert.Equal(0, deletedCount);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void BestEffortCleanup_WithFewerFilesThanRetention_DeletesNothing()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            CreateLogFiles(tempDir, "app", 5);

            // Act
            var (success, deletedCount, errorMessage) = LogRetentionCleaner.BestEffortCleanup(
                tempDir, "app", 20);

            // Assert
            Assert.True(success);
            Assert.Equal(0, deletedCount);
            Assert.Null(errorMessage);
            Assert.Equal(5, Directory.GetFiles(tempDir, "app-*.jsonl").Length);
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void BestEffortCleanup_WithMoreFilesThanRetention_DeletesOldest()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            var files = CreateLogFiles(tempDir, "test", 30);

            // Act
            var (success, deletedCount, errorMessage) = LogRetentionCleaner.BestEffortCleanup(
                tempDir, "test", 20);

            // Assert
            Assert.True(success);
            Assert.Equal(10, deletedCount);
            Assert.Null(errorMessage);
            Assert.Equal(20, Directory.GetFiles(tempDir, "test-*.jsonl").Length);
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void BestEffortCleanup_KeepsMostRecentFiles()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            var files = CreateLogFiles(tempDir, "app", 10);
            
            // Mark the last 3 files as most recent
            var newestFiles = files.OrderBy(f => f).TakeLast(3).ToArray();
            foreach (var file in newestFiles)
            {
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
            }

            // Act
            var (success, deletedCount, errorMessage) = LogRetentionCleaner.BestEffortCleanup(
                tempDir, "app", 3);

            // Assert
            Assert.True(success);
            Assert.Equal(7, deletedCount);
            Assert.Null(errorMessage);
            
            var remainingFiles = Directory.GetFiles(tempDir, "app-*.jsonl");
            Assert.Equal(3, remainingFiles.Length);
            
            // Verify the newest files are still there
            foreach (var newestFile in newestFiles)
            {
                Assert.Contains(remainingFiles, f => f == newestFile);
            }
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void BestEffortCleanup_WithZeroRetention_ReturnsError()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            CreateLogFiles(tempDir, "test", 5);

            // Act
            var (success, deletedCount, errorMessage) = LogRetentionCleaner.BestEffortCleanup(
                tempDir, "test", 0);

            // Assert
            Assert.False(success);
            Assert.Equal(0, deletedCount);
            Assert.Contains("positive", errorMessage);
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    [Fact]
    public void BestEffortCleanup_OnlyDeletesMatchingPrefix()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            CreateLogFiles(tempDir, "app1", 15);
            CreateLogFiles(tempDir, "app2", 15);

            // Act
            var (success, deletedCount, errorMessage) = LogRetentionCleaner.BestEffortCleanup(
                tempDir, "app1", 10);

            // Assert
            Assert.True(success);
            Assert.Equal(5, deletedCount);
            Assert.Null(errorMessage);
            Assert.Equal(10, Directory.GetFiles(tempDir, "app1-*.jsonl").Length);
            Assert.Equal(15, Directory.GetFiles(tempDir, "app2-*.jsonl").Length);
        }
        finally
        {
            CleanupTempDirectory(tempDir);
        }
    }

    // Helper methods

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LogRetentionTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void CleanupTempDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private static List<string> CreateLogFiles(string directory, string prefix, int count)
    {
        var files = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var fileName = $"{prefix}-{DateTime.UtcNow.Ticks + i}.jsonl";
            var filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, $"Log entry {i}");
            
            // Set different timestamps to ensure sorting works
            File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(-count + i));
            files.Add(filePath);
            
            // Small delay to ensure different timestamps
            Thread.Sleep(1);
        }
        return files;
    }
}
