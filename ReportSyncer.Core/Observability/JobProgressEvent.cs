#region License

// author:         Gary Wu
// created:        18:12
// description:    Job-level progress notification with optional aggregated metrics.

#endregion

namespace ReportSyncer.Core.Observability;

/// <summary>
/// Job-level progress event. May include aggregated real-time metrics.
/// </summary>
public sealed record JobProgressEvent(
    string            JobId,
    ProgressEventKind Kind,
    ProgressPhase     Phase,
    DateTimeOffset    UtcTimestamp,
    TimeSpan?         Elapsed      = null,
    
    /// <summary>
    /// Aggregated progress metrics across all tables. Used with Kind=InProgress. Null for lifecycle events.
    /// </summary>
    ProgressMetrics?  Metrics      = null,
    
    string?           Message      = null,
    string?           ErrorCode    = null,
    string?           ErrorMessage = null);
