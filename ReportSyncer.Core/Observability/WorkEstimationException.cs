// ============================================================================
// File: WorkEstimationException.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Exception thrown when work estimation fails.
// ============================================================================

namespace ReportSyncer.Core.Observability;

/// <summary>
/// Represents an error that occurs while estimating work for a table sync.
/// </summary>
public sealed class WorkEstimationException : Exception
{
    public WorkEstimationException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
