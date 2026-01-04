// ============================================================================
// File: LogFilePathProviderTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Tests for LogFilePathProvider.
// ============================================================================

using ReportSyncer.Console.Logging;
using Xunit;

namespace ReportSyncer.Console.Tests.Logging;

public sealed class LogFilePathProviderTests
{
    [Fact]
    public void BuildJsonlPath_WithValidInputs_ReturnsCorrectPath()
    {
        // Arrange
        var directory = "logs";
        var prefix = "reportsyncer";
        var now = new DateTimeOffset(2026, 1, 4, 14, 30, 25, 123, TimeSpan.Zero);

        // Act
        var path = LogFilePathProvider.BuildJsonlPath(directory, prefix, now);

        // Assert
        Assert.EndsWith(".jsonl", path);
        Assert.Contains("reportsyncer-20260104-143025-123.jsonl", path);
        Assert.Contains("logs", path);
    }

    [Fact]
    public void BuildJsonlPath_WithTrailingSlash_HandlesCorrectly()
    {
        // Arrange
        var directory = "logs/";
        var prefix = "test";
        var now = new DateTimeOffset(2026, 1, 4, 10, 20, 30, 456, TimeSpan.Zero);

        // Act
        var path = LogFilePathProvider.BuildJsonlPath(directory, prefix, now);

        // Assert
        Assert.DoesNotContain("//", path);
        Assert.Contains("test-20260104-102030-456.jsonl", path);
    }

    [Fact]
    public void BuildJsonlPath_WithBackslashes_NormalizesPath()
    {
        // Arrange
        var directory = "logs\\subfolder";
        var prefix = "app";
        var now = new DateTimeOffset(2026, 12, 31, 23, 59, 59, 999, TimeSpan.Zero);

        // Act
        var path = LogFilePathProvider.BuildJsonlPath(directory, prefix, now);

        // Assert
        Assert.Contains("app-20261231-235959-999.jsonl", path);
    }

    [Fact]
    public void BuildJsonlPath_WithNullDirectory_ThrowsArgumentException()
    {
        // Arrange
        var now = DateTimeOffset.Now;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            LogFilePathProvider.BuildJsonlPath(null!, "prefix", now));
        Assert.Contains("Directory", ex.Message);
    }

    [Fact]
    public void BuildJsonlPath_WithEmptyPrefix_ThrowsArgumentException()
    {
        // Arrange
        var now = DateTimeOffset.Now;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            LogFilePathProvider.BuildJsonlPath("logs", "", now));
        Assert.Contains("prefix", ex.Message);
    }

    [Fact]
    public void BuildJsonlPath_SameTimestamp_ProducesSamePath()
    {
        // Arrange
        var directory = "logs";
        var prefix = "test";
        var now = new DateTimeOffset(2026, 6, 15, 12, 0, 0, 0, TimeSpan.Zero);

        // Act
        var path1 = LogFilePathProvider.BuildJsonlPath(directory, prefix, now);
        var path2 = LogFilePathProvider.BuildJsonlPath(directory, prefix, now);

        // Assert
        Assert.Equal(path1, path2);
    }
}
