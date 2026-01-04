// -----------------------------------------------------------------------------
// <copyright file="ExitCodeCombinerTests.cs" company="Gary Wu">
// Copyright (c) 2026 Gary Wu. All rights reserved.
// </copyright>
// <author>Gary Wu</author>
// <date>2026-01-04</date>
// <summary>
// Unit tests for ExitCodeCombiner.
// </summary>
// -----------------------------------------------------------------------------

using ReportSyncer.Console.ExitCodes;
using Xunit;

namespace ReportSyncer.Console.Tests.ExitCodes;

/// <summary>
/// Tests for <see cref="ExitCodeCombiner"/>.
/// </summary>
public class ExitCodeCombinerTests
{
    /// <summary>
    /// Verifies that the severity order is correctly applied: 0 &lt; 2 &lt; 3 &lt; 4 &lt; 5 &lt; 6 &lt; 1.
    /// </summary>
    [Fact]
    public void CombineWorst_ShouldReturnWorstExitCode_AccordingToSeverityOrder()
    {
        // Arrange & Act & Assert
        // Success (0) vs InvalidArguments (2) -> InvalidArguments
        Assert.Equal(ExitCode.InvalidArguments,
            ExitCodeCombiner.CombineWorst(ExitCode.Success, ExitCode.InvalidArguments));

        // InvalidArguments (2) vs InvalidConfiguration (3) -> InvalidConfiguration
        Assert.Equal(ExitCode.InvalidConfiguration,
            ExitCodeCombiner.CombineWorst(ExitCode.InvalidArguments, ExitCode.InvalidConfiguration));

        // InvalidConfiguration (3) vs PreflightFailed (4) -> PreflightFailed
        Assert.Equal(ExitCode.PreflightFailed,
            ExitCodeCombiner.CombineWorst(ExitCode.InvalidConfiguration, ExitCode.PreflightFailed));

        // PreflightFailed (4) vs ExecutionFailed (5) -> ExecutionFailed
        Assert.Equal(ExitCode.ExecutionFailed,
            ExitCodeCombiner.CombineWorst(ExitCode.PreflightFailed, ExitCode.ExecutionFailed));

        // ExecutionFailed (5) vs Cancelled (6) -> Cancelled
        Assert.Equal(ExitCode.Cancelled,
            ExitCodeCombiner.CombineWorst(ExitCode.ExecutionFailed, ExitCode.Cancelled));

        // Cancelled (6) vs UnhandledFatal (1) -> UnhandledFatal (most severe)
        Assert.Equal(ExitCode.UnhandledFatal,
            ExitCodeCombiner.CombineWorst(ExitCode.Cancelled, ExitCode.UnhandledFatal));
    }

    /// <summary>
    /// Verifies that UnhandledFatal is always the worst, regardless of order.
    /// </summary>
    [Fact]
    public void CombineWorst_UnhandledFatal_ShouldAlwaysBeWorst()
    {
        // Arrange
        var allCodes = new[]
        {
            ExitCode.Success,
            ExitCode.InvalidArguments,
            ExitCode.InvalidConfiguration,
            ExitCode.PreflightFailed,
            ExitCode.ExecutionFailed,
            ExitCode.Cancelled
        };

        // Act & Assert
        foreach (var code in allCodes)
        {
            Assert.Equal(ExitCode.UnhandledFatal,
                ExitCodeCombiner.CombineWorst(code, ExitCode.UnhandledFatal));
            Assert.Equal(ExitCode.UnhandledFatal,
                ExitCodeCombiner.CombineWorst(ExitCode.UnhandledFatal, code));
        }
    }

    /// <summary>
    /// Verifies that combining the same exit code returns that exit code.
    /// </summary>
    [Fact]
    public void CombineWorst_SameExitCode_ShouldReturnThatCode()
    {
        // Arrange & Act & Assert
        Assert.Equal(ExitCode.Success, ExitCodeCombiner.CombineWorst(ExitCode.Success, ExitCode.Success));
        Assert.Equal(ExitCode.InvalidArguments, ExitCodeCombiner.CombineWorst(ExitCode.InvalidArguments, ExitCode.InvalidArguments));
        Assert.Equal(ExitCode.Cancelled, ExitCodeCombiner.CombineWorst(ExitCode.Cancelled, ExitCode.Cancelled));
        Assert.Equal(ExitCode.UnhandledFatal, ExitCodeCombiner.CombineWorst(ExitCode.UnhandledFatal, ExitCode.UnhandledFatal));
    }

    /// <summary>
    /// Verifies that order of arguments doesn't matter (commutative property).
    /// </summary>
    [Fact]
    public void CombineWorst_ShouldBeCommutative()
    {
        // Arrange
        var testCases = new[]
        {
            (ExitCode.Success, ExitCode.ExecutionFailed),
            (ExitCode.InvalidArguments, ExitCode.PreflightFailed),
            (ExitCode.Cancelled, ExitCode.InvalidConfiguration)
        };

        // Act & Assert
        foreach (var (first, second) in testCases)
        {
            Assert.Equal(
                ExitCodeCombiner.CombineWorst(first, second),
                ExitCodeCombiner.CombineWorst(second, first));
        }
    }

    /// <summary>
    /// Verifies sequential combination produces the worst overall code.
    /// </summary>
    [Fact]
    public void CombineWorst_SequentialCombination_ShouldProduceWorstOverall()
    {
        // Arrange
        var current = ExitCode.Success;

        // Act
        current = ExitCodeCombiner.CombineWorst(current, ExitCode.InvalidArguments);
        current = ExitCodeCombiner.CombineWorst(current, ExitCode.ExecutionFailed);
        current = ExitCodeCombiner.CombineWorst(current, ExitCode.PreflightFailed);
        current = ExitCodeCombiner.CombineWorst(current, ExitCode.InvalidConfiguration);

        // Assert - ExecutionFailed (5) is worst among the non-fatal codes combined
        Assert.Equal(ExitCode.ExecutionFailed, current);

        // Act - Add UnhandledFatal
        current = ExitCodeCombiner.CombineWorst(current, ExitCode.UnhandledFatal);

        // Assert - UnhandledFatal wins
        Assert.Equal(ExitCode.UnhandledFatal, current);
    }
}
