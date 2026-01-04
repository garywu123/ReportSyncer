// ============================================================================
// File: CliArgumentParserTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Unit tests for CLI argument parsing.
// ============================================================================

using ReportSyncer.Console.Hosting;

namespace ReportSyncer.Console.Tests.Hosting;

public sealed class CliArgumentParserTests
{
    [Fact]
    public void TryParse_NoArguments_ReturnsSuccessWithNullValues()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Null(options.ConfigPath);
        Assert.Null(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_UnknownArgument_ReturnsError()
    {
        // Arrange
        var args = new[] { "--wat" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.False(ok);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains("--config", error);
        Assert.Contains("--dry-run", error);
    }

    [Fact]
    public void TryParse_ConfigWithSpace_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--config", "path/to/job.yaml" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("path/to/job.yaml", options.ConfigPath);
        Assert.Null(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_ConfigWithEquals_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--config=path/to/job.yaml" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("path/to/job.yaml", options.ConfigPath);
        Assert.Null(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_ConfigMissingValue_ReturnsError()
    {
        // Arrange
        var args = new[] { "--config" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.False(ok);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains("Missing value for --config", error);
    }

    [Fact]
    public void TryParse_ConfigEmptyValue_ReturnsError()
    {
        // Arrange
        var args = new[] { "--config", "   " };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.False(ok);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains("--config cannot be empty", error);
    }

    [Fact]
    public void TryParse_DryRunNoValue_DefaultsToTrue()
    {
        // Arrange
        var args = new[] { "--dry-run" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Null(options.ConfigPath);
        Assert.True(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_DryRunTrueWithSpace_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--dry-run", "true" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.True(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_DryRunFalseWithSpace_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--dry-run", "false" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.False(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_DryRunTrueWithEquals_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--dry-run=true" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.True(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_DryRunFalseWithEquals_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--dry-run=false" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.False(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_DryRunInvalidValue_ReturnsError()
    {
        // Arrange
        var args = new[] { "--dry-run=maybe" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.False(ok);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains("Invalid value 'maybe'", error);
    }

    [Fact]
    public void TryParse_BothArguments_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--config", "my.yaml", "--dry-run", "true" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("my.yaml", options.ConfigPath);
        Assert.True(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_DuplicateConfig_LastWins()
    {
        // Arrange
        var args = new[] { "--config", "first.yaml", "--config", "second.yaml" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("second.yaml", options.ConfigPath);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_DuplicateDryRun_LastWins()
    {
        // Arrange
        var args = new[] { "--dry-run", "true", "--dry-run", "false" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.False(options.DryRun);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_CaseInsensitive_ParsesCorrectly()
    {
        // Arrange
        var args = new[] { "--CONFIG", "file.yaml", "--DRY-RUN", "TRUE" };

        // Act
        var (ok, options, error) = CliArgumentParser.TryParse(args);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("file.yaml", options.ConfigPath);
        Assert.True(options.DryRun);
        Assert.Null(error);
    }
}
