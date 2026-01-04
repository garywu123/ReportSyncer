// ============================================================================
// File: PageInfo.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: Page information for display purposes.
// ============================================================================

namespace ReportSyncer.Console.UI;

/// <summary>
/// Information about the current page for display.
/// </summary>
/// <param name="PageIndex">Zero-based page index.</param>
/// <param name="PageCount">Total number of pages.</param>
/// <param name="StartRowNumber">One-based start row number.</param>
/// <param name="EndRowNumber">One-based end row number.</param>
/// <param name="TotalCount">Total row count.</param>
public sealed record PageInfo(
    int PageIndex,
    int PageCount,
    int StartRowNumber,
    int EndRowNumber,
    int TotalCount);
