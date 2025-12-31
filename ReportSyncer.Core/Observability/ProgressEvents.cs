// ============================================================================
// File: ProgressEvents.cs
// Author: Codex
// Project: ReportSyncer
// Description: Progress event contracts for job and table execution.
// ============================================================================

namespace ReportSyncer.Core.Observability;

/// <summary>
/// High-level kinds for progress events. Use <see cref="InProgress"/> for real-time updates.
/// </summary>
public enum ProgressEventKind
{
    Started,
    InProgress,
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