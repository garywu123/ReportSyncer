// ============================================================================
// File: ProgressEvents.cs
// Author: Codex
// Project: ReportSyncer
// Description: Progress event contracts for job and table execution.
// ============================================================================

using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Observability;

/// <summary>
/// High-level event kind used for job/table progress.
/// </summary>
public enum ProgressEventKind
{
    Started,
    Completed,
    Skipped,
    Failed
}

/// <summary>
/// Job-level phases for reporting.
/// </summary>
public enum ProgressPhase
{
    Preflight,
    Execution
}

/// <summary>
/// Progress notification for a job lifecycle.
/// </summary>
public sealed record JobProgressEvent(
    string JobId,
    ProgressEventKind Kind,
    ProgressPhase Phase,
    DateTimeOffset UtcTimestamp,
    TimeSpan? Elapsed = null,
    string? Message = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>
/// Progress notification for a table-phase lifecycle.
/// </summary>
public sealed record TableProgressEvent(
    string JobId,
    TableIdentifier Table,
    ProgressEventKind Kind,
    SyncPhase Phase,
    DateTimeOffset UtcTimestamp,
    TimeSpan? Elapsed = null,
    int? RowsAffected = null,
    bool IsDryRun = false,
    string? Message = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
