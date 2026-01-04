// ============================================================================
// File: UiEventKind.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: Enum representing the type of UI event.
// ============================================================================

namespace ReportSyncer.Console.UI;

/// <summary>
/// Represents the kind of UI event being enqueued.
/// </summary>
public enum UiEventKind
{
    /// <summary>
    /// Job-level progress event.
    /// </summary>
    Job,
    
    /// <summary>
    /// Table-level progress event.
    /// </summary>
    Table
}
