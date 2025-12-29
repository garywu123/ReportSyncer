// ============================================================================
// File: PreflightGateException.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Exception wrapper for failures during preflight gate evaluation.
// ============================================================================

namespace ReportSyncer.Core.Preflight;

/// <summary>
/// Wraps failures that occur while executing the preflight gate pipeline.
/// </summary>
public sealed class PreflightGateException : Exception
{
    public PreflightGateException(string message, Exception? inner = null)
        : base(message, inner) { }
}
