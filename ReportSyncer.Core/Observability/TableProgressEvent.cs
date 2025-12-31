#region License

// author:         Gary Wu
// created:        18:12
// description:    Progress notification for a table-phase lifecycle with optional real-time metrics.

#endregion

using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Observability;

/// <summary>
/// Table-level progress event. Optionally carries real-time metrics (see <see cref="ProgressMetrics"/>).
/// </summary>
public sealed record TableProgressEvent(
    string            JobId,
    TableIdentifier   Table,
    ProgressEventKind Kind,
    SyncPhase         Phase,
    DateTimeOffset    UtcTimestamp,
    TimeSpan?         Elapsed      = null,
    int?              RowsAffected = null,
    bool              IsDryRun     = false,
    ProgressMetrics?  Metrics      = null,
    string?           Message      = null,
    string?           ErrorCode    = null,
    string?           ErrorMessage = null);
