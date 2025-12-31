// ============================================================================
// File: ProgressEvents.cs
// Author: Codex
// Project: ReportSyncer
// Description: Progress event contracts for job and table execution.
// ============================================================================

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