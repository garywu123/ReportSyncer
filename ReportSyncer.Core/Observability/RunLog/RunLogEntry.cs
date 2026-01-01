// ============================================================================
// File: RunLogEntry.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Defines the structure of a single line in a jsonl run log file.
//              Each entry represents a structured event with correlation
//              dimensions, metrics, and optional error information.
// ============================================================================

using System.Text.Json.Serialization;

namespace ReportSyncer.Core.Observability.RunLog;

/// <summary>
/// Represents a single structured log entry in a run log (jsonl format).
/// Each entry is serialized as a single line of JSON with consistent field naming.
/// </summary>
/// <remarks>
/// Field naming uses camelCase for JSON serialization to match common observability
/// platform conventions. All entries must include utcTimestamp, level, jobId, and eventKind.
/// Table-level entries additionally include table and syncPhase.
/// </remarks>
public sealed record RunLogEntry
{
    /// <summary>
    /// UTC timestamp of the event in ISO 8601 format.
    /// </summary>
    [JsonPropertyName("utcTimestamp")]
    public required string UtcTimestamp { get; init; }

    /// <summary>
    /// Log level: Info, Debug, Warn, or Error.
    /// </summary>
    [JsonPropertyName("level")]
    public required string Level { get; init; }

    /// <summary>
    /// Job identifier for correlation.
    /// </summary>
    [JsonPropertyName("jobId")]
    public required string JobId { get; init; }

    /// <summary>
    /// Event kind: Started, InProgress, Completed, Skipped, Failed.
    /// </summary>
    [JsonPropertyName("eventKind")]
    public required string EventKind { get; init; }

    /// <summary>
    /// Job phase (Preflight, Execution) or sync phase (Preflight, Estimate, Delete, Insert).
    /// </summary>
    [JsonPropertyName("phase")]
    public required string Phase { get; init; }

    /// <summary>
    /// Table identifier (for table-level events). Null for job-level events.
    /// </summary>
    [JsonPropertyName("table")]
    public string? Table { get; init; }

    /// <summary>
    /// Elapsed time in milliseconds since event start.
    /// </summary>
    [JsonPropertyName("elapsedMs")]
    public long? ElapsedMs { get; init; }

    /// <summary>
    /// Number of rows affected (for table-level events).
    /// </summary>
    [JsonPropertyName("rowsAffected")]
    public int? RowsAffected { get; init; }

    /// <summary>
    /// Whether this was a dry-run execution.
    /// </summary>
    [JsonPropertyName("isDryRun")]
    public bool IsDryRun { get; init; }

    /// <summary>
    /// Progress metrics (for InProgress events).
    /// </summary>
    [JsonPropertyName("metrics")]
    public MetricsData? Metrics { get; init; }

    /// <summary>
    /// Optional human-readable message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Error code (for Failed events).
    /// </summary>
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Error message (for Failed events).
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Flattened metrics data to ensure JSON serialization compatibility.
    /// </summary>
    public sealed record MetricsData
    {
        [JsonPropertyName("rowsProcessed")]
        public long RowsProcessed { get; init; }

        [JsonPropertyName("totalRowsPlanned")]
        public long? TotalRowsPlanned { get; init; }

        [JsonPropertyName("percentComplete")]
        public double? PercentComplete { get; init; }

        [JsonPropertyName("throughputRowsPerSec")]
        public double? ThroughputRowsPerSec { get; init; }

        [JsonPropertyName("etaSeconds")]
        public long? EtaSeconds { get; init; }
    }
}
