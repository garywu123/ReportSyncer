#region License

// author:         GWu
// created:        18:12
// description:

#endregion

using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Observability;

/// <summary>
/// Progress notification for a table-phase lifecycle.
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
    string?           Message      = null,
    string?           ErrorCode    = null,
    string?           ErrorMessage = null);
