// ============================================================================
// File: UiEvent.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: Unified event wrapper for UI pipeline.
// ============================================================================

namespace ReportSyncer.Console.UI;

/// <summary>
/// Wrapper for Core progress events flowing through the UI pipeline.
/// </summary>
/// <param name="Kind">Type of event (Job or Table).</param>
/// <param name="Payload">Actual event object (JobProgressEvent or TableProgressEvent).</param>
/// <param name="AtUtc">Timestamp when event was enqueued.</param>
public sealed record UiEvent(
    UiEventKind Kind,
    object Payload,
    DateTimeOffset AtUtc);
