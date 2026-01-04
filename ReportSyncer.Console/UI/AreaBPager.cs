// ============================================================================
// File: AreaBPager.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: Paging logic for Area B table display.
// ============================================================================

namespace ReportSyncer.Console.UI;

/// <summary>
/// Handles paging logic for Area B. Clamps PageIndex and selects page slice.
/// Does NOT perform sorting - that's the caller's responsibility.
/// </summary>
/// <remarks>
/// CRITICAL RULE: This class is the SINGLE source of truth for page clamping logic.
/// All page index clamping must happen here to avoid inconsistent behavior.
/// </remarks>
public sealed class AreaBPager
{
    private const int DefaultPageSize = 20;
    private int _pageIndex;

    /// <summary>
    /// Gets or sets the page size. Values &lt;= 0 are normalized to 20.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets or sets the current page index (zero-based).
    /// Automatically clamped when GetPageInfo or SelectPage is called.
    /// </summary>
    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AreaBPager"/>.
    /// </summary>
    /// <param name="pageSize">Page size (default 20). Values &lt;= 0 are normalized to 20.</param>
    public AreaBPager(int pageSize = DefaultPageSize)
    {
        PageSize = pageSize <= 0 ? DefaultPageSize : pageSize;
        _pageIndex = 0;
    }

    /// <summary>
    /// Gets page information with clamped PageIndex.
    /// </summary>
    /// <param name="totalCount">Total number of rows.</param>
    /// <returns>Page information for display.</returns>
    public PageInfo GetPageInfo(int totalCount)
    {
        if (totalCount < 0)
            totalCount = 0;

        // Calculate page count (minimum 1)
        int pageCount = Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));

        // Clamp page index
        int clampedIndex = Math.Clamp(_pageIndex, 0, pageCount - 1);
        _pageIndex = clampedIndex;

        // Calculate row numbers (1-based)
        int startRowNumber = totalCount == 0 ? 0 : (clampedIndex * PageSize) + 1;
        int endRowNumber = totalCount == 0 ? 0 : Math.Min((clampedIndex + 1) * PageSize, totalCount);

        return new PageInfo(
            PageIndex: clampedIndex,
            PageCount: pageCount,
            StartRowNumber: startRowNumber,
            EndRowNumber: endRowNumber,
            TotalCount: totalCount);
    }

    /// <summary>
    /// Selects the current page from the input rows.
    /// </summary>
    /// <param name="rows">All rows (must already be sorted by caller).</param>
    /// <returns>Rows for the current page.</returns>
    public IReadOnlyList<TableRowState> SelectPage(IReadOnlyList<TableRowState> rows)
    {
        if (rows is null || rows.Count == 0)
            return Array.Empty<TableRowState>();

        // Get clamped page info
        var pageInfo = GetPageInfo(rows.Count);

        // Skip and take
        return rows
            .Skip(pageInfo.PageIndex * PageSize)
            .Take(PageSize)
            .ToList();
    }
}
