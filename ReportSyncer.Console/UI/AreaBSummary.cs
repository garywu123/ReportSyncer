// ============================================================================
// File: AreaBSummary.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: Summary counts for Area B display.
// ============================================================================

namespace ReportSyncer.Console.UI;

/// <summary>
/// Summary counts for Area B table status display.
/// </summary>
/// <param name="Total">Total number of tables.</param>
/// <param name="Running">Number of tables currently running.</param>
/// <param name="Completed">Number of completed tables.</param>
/// <param name="Failed">Number of failed tables.</param>
/// <param name="Skipped">Number of skipped tables.</param>
public sealed record AreaBSummary(
    int Total,
    int Running,
    int Completed,
    int Failed,
    int Skipped);
