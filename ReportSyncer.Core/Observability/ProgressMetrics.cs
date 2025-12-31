namespace ReportSyncer.Core.Observability;

/// <summary>
/// Compact record for real-time progress metrics. Nullable fields indicate unavailable values.
/// Use with <see cref="ProgressEventKind.InProgress"/>.
/// </summary>
public record ProgressMetrics(
    long RowsProcessed,
    long? TotalRowsPlanned,
    double? PercentComplete,
    double? ThroughputRowsPerSec,
    long? EtaSeconds
);
