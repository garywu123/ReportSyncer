// ============================================================================
// File: RunLogLayoutSpecTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Unit tests for RunLogLayoutSpec path generation and sanitization.
// ============================================================================

using FluentAssertions;
using ReportSyncer.Core.Observability.RunLog;
using ReportSyncer.Core.Schema;
using Xunit;

namespace ReportSyncer.Core.Tests.Observability.RunLog;

public sealed class RunLogLayoutSpecTests
{
    [Fact]
    public void GetRunDirectory_WithValidInputs_ReturnsExpectedPath()
    {
        // Arrange
        var baseDir = "/logs";
        var layout = new RunLogLayoutSpec(baseDir);
        var jobId = "daily-sync";
        var runId = "20260101_120000_abc123";

        // Act
        var result = layout.GetRunDirectory(jobId, runId);

        // Assert
        result.Should().Be(Path.Combine(baseDir, jobId, runId));
    }

    [Fact]
    public void GetJobLogPath_WithValidInputs_ReturnsExpectedPath()
    {
        // Arrange
        var baseDir = "/logs";
        var layout = new RunLogLayoutSpec(baseDir);
        var jobId = "daily-sync";
        var runId = "20260101_120000_abc123";

        // Act
        var result = layout.GetJobLogPath(jobId, runId);

        // Assert
        result.Should().Be(Path.Combine(baseDir, jobId, runId, "job.jsonl"));
    }

    [Fact]
    public void GetTableLogPath_WithValidInputs_ReturnsExpectedPath()
    {
        // Arrange
        var baseDir = "/logs";
        var layout = new RunLogLayoutSpec(baseDir);
        var jobId = "daily-sync";
        var runId = "20260101_120000_abc123";
        var table = TableIdentifier.Parse("dbo.Users");

        // Act
        var result = layout.GetTableLogPath(jobId, runId, table);

        // Assert
        result.Should().Be(Path.Combine(baseDir, jobId, runId, "tables", "dbo.Users.jsonl"));
    }

    [Theory]
    [InlineData("Job A/01", "Job_A_01")]
    [InlineData("User Role", "User_Role")]
    [InlineData("dbo.Table-Name", "dbo.Table-Name")]
    [InlineData("test@server", "test_server")]
    [InlineData("path\\to\\file", "path_to_file")]
    [InlineData("valid_name-123.test", "valid_name-123.test")]
    public void Sanitize_WithVariousInputs_ReplacesInvalidCharacters(string input, string expected)
    {
        // Act
        var result = RunLogLayoutSpec.Sanitize(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_WithNullOrWhitespace_ReturnsUnderscore(string? input)
    {
        // Act
        var result = RunLogLayoutSpec.Sanitize(input!);

        // Assert
        result.Should().Be("_");
    }

    [Fact]
    public void GetTableLogPath_EnsuresTablesSubdirectory()
    {
        // Arrange
        var baseDir = "/logs";
        var layout = new RunLogLayoutSpec(baseDir);
        var jobId = "test-job";
        var runId = "20260101_120000_abc123";
        var table = TableIdentifier.Parse("dbo.Products");

        // Act
        var result = layout.GetTableLogPath(jobId, runId, table);

        // Assert
        result.Should().Contain(Path.Combine("tables", "dbo.Products.jsonl"));
    }

    [Fact]
    public void GetRunDirectory_WithJobIdContainingSpecialCharacters_SanitizesPath()
    {
        // Arrange
        var baseDir = "/logs";
        var layout = new RunLogLayoutSpec(baseDir);
        var jobId = "Job A/B\\C:D";
        var runId = "test_run";

        // Act
        var result = layout.GetRunDirectory(jobId, runId);

        // Assert - extract just the sanitized job ID part (platform-independent)
        var sanitizedJobId = result.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .FirstOrDefault(p => p.Contains("Job"));
        
        sanitizedJobId.Should().Be("Job_A_B_C_D");
    }
}
