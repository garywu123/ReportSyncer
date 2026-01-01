// ============================================================================
// File: CorrelationLogContractTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Unit tests for CorrelationLogContract validation rules.
// ============================================================================

using FluentAssertions;
using ReportSyncer.Core.Observability.RunLog;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Sync.Contracts;
using Xunit;

namespace ReportSyncer.Core.Tests.Observability.RunLog;

public sealed class CorrelationLogContractTests
{
    [Fact]
    public void ValidateJob_WithValidInputs_DoesNotThrow()
    {
        // Arrange
        var jobId = "test-job";
        var phase = "Execution";

        // Act
        var act = () => CorrelationLogContract.ValidateJob(jobId, phase);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null, "Execution")]
    [InlineData("", "Execution")]
    [InlineData("   ", "Execution")]
    public void ValidateJob_WithInvalidJobId_ThrowsInvalidOperationException(string? jobId, string phase)
    {
        // Act
        var act = () => CorrelationLogContract.ValidateJob(jobId!, phase);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Correlation requires JobId.");
    }

    [Theory]
    [InlineData("test-job", null)]
    [InlineData("test-job", "")]
    [InlineData("test-job", "   ")]
    public void ValidateJob_WithInvalidPhase_ThrowsInvalidOperationException(string jobId, string? phase)
    {
        // Act
        var act = () => CorrelationLogContract.ValidateJob(jobId, phase!);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Correlation requires Phase.");
    }

    [Fact]
    public void ValidateTable_WithValidInputs_DoesNotThrow()
    {
        // Arrange
        var jobId = "test-job";
        var table = TableIdentifier.Parse("dbo.Users");
        var phase = SyncPhase.Insert;

        // Act
        var act = () => CorrelationLogContract.ValidateTable(jobId, table, phase);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateTable_WithInvalidJobId_ThrowsInvalidOperationException(string? jobId)
    {
        // Arrange
        var table = TableIdentifier.Parse("dbo.Users");
        var phase = SyncPhase.Insert;

        // Act
        var act = () => CorrelationLogContract.ValidateTable(jobId!, table, phase);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Correlation requires JobId.");
    }

    [Fact]
    public void ValidateTable_WithNullTable_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job";
        TableIdentifier? table = null;
        var phase = SyncPhase.Insert;

        // Act
        var act = () => CorrelationLogContract.ValidateTable(jobId, table!, phase);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Correlation requires TableIdentifier.");
    }
}
