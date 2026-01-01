// ============================================================================
// File: RunLogEntryTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Unit tests for RunLogEntry serialization and structure.
// ============================================================================

using System.Text.Json;
using FluentAssertions;
using ReportSyncer.Core.Observability.RunLog;
using Xunit;

namespace ReportSyncer.Core.Tests.Observability.RunLog;

public sealed class RunLogEntryTests
{
    [Fact]
    public void SerializeEntry_WithMinimalFields_ProducesValidJson()
    {
        // Arrange
        var entry = new RunLogEntry
        {
            UtcTimestamp = "2026-01-01T12:00:00.000Z",
            Level = "Info",
            JobId = "test-job",
            EventKind = "Started",
            Phase = "Execution"
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(entry, options);

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"utcTimestamp\"");
        json.Should().Contain("\"level\"");
        json.Should().Contain("\"jobId\"");
        json.Should().Contain("\"eventKind\"");
        json.Should().Contain("\"phase\"");
    }

    [Fact]
    public void SerializeEntry_WithAllFields_ProducesValidJson()
    {
        // Arrange
        var entry = new RunLogEntry
        {
            UtcTimestamp = "2026-01-01T12:00:00.000Z",
            Level = "Error",
            JobId = "test-job",
            EventKind = "Failed",
            Phase = "Insert",
            Table = "dbo.Users",
            ElapsedMs = 1234,
            RowsAffected = 100,
            IsDryRun = true,
            Metrics = new RunLogEntry.MetricsData
            {
                RowsProcessed = 500,
                TotalRowsPlanned = 1000,
                PercentComplete = 50.0,
                ThroughputRowsPerSec = 100.5,
                EtaSeconds = 10
            },
            Message = "Test message",
            ErrorCode = "ERR001",
            ErrorMessage = "Test error"
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(entry, options);

        // Assert
        json.Should().Contain("\"table\":\"dbo.Users\"");
        json.Should().Contain("\"elapsedMs\":1234");
        json.Should().Contain("\"rowsAffected\":100");
        json.Should().Contain("\"isDryRun\":true");
        json.Should().Contain("\"rowsProcessed\":500");
        json.Should().Contain("\"errorCode\":\"ERR001\"");
    }

    [Fact]
    public void SerializeEntry_ProducesSingleLineJson()
    {
        // Arrange
        var entry = new RunLogEntry
        {
            UtcTimestamp = "2026-01-01T12:00:00.000Z",
            Level = "Info",
            JobId = "test-job",
            EventKind = "Completed",
            Phase = "Execution"
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(entry, options);

        // Assert
        json.Should().NotContain(Environment.NewLine);
        json.Should().NotContain("\n");
        json.Should().NotContain("\r");
    }

    [Fact]
    public void DeserializeEntry_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new RunLogEntry
        {
            UtcTimestamp = "2026-01-01T12:00:00.000Z",
            Level = "Info",
            JobId = "test-job",
            EventKind = "InProgress",
            Phase = "Delete",
            Table = "dbo.Orders",
            ElapsedMs = 500,
            RowsAffected = 25,
            IsDryRun = false,
            Message = "Processing..."
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<RunLogEntry>(json, options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.JobId.Should().Be(original.JobId);
        deserialized.Level.Should().Be(original.Level);
        deserialized.EventKind.Should().Be(original.EventKind);
        deserialized.Table.Should().Be(original.Table);
        deserialized.ElapsedMs.Should().Be(original.ElapsedMs);
        deserialized.RowsAffected.Should().Be(original.RowsAffected);
        deserialized.IsDryRun.Should().Be(original.IsDryRun);
    }

    [Fact]
    public void MetricsData_Serializes_WithCamelCase()
    {
        // Arrange
        var metrics = new RunLogEntry.MetricsData
        {
            RowsProcessed = 1000,
            TotalRowsPlanned = 2000,
            PercentComplete = 50.0,
            ThroughputRowsPerSec = 200.5,
            EtaSeconds = 5
        };

        var entry = new RunLogEntry
        {
            UtcTimestamp = "2026-01-01T12:00:00.000Z",
            Level = "Debug",
            JobId = "test-job",
            EventKind = "InProgress",
            Phase = "Insert",
            Metrics = metrics
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(entry, options);

        // Assert
        json.Should().Contain("\"rowsProcessed\":1000");
        json.Should().Contain("\"totalRowsPlanned\":2000");
        json.Should().Contain("\"percentComplete\":50");
        json.Should().Contain("\"throughputRowsPerSec\":200.5");
        json.Should().Contain("\"etaSeconds\":5");
    }
}
