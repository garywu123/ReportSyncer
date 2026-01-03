// ============================================================================
// File: FilterConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for composite filtering (date range + key filters).
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// <summary>
/// Filter configuration for table sync tasks.
/// </summary>
/// <remarks>
/// <example>
/// </example>
public class FilterConfig
{
    /// <summary>
    /// Gets the column name used for date range filtering.
    /// Must be a datetime/date column in both source and target tables.
    /// Null when no date range filter is applied.
    /// </summary>
    public string? DateColumn { get; }

    /// <summary>
    /// Gets the start date for the date range filter (inclusive).
    /// Supports parameter placeholders like "{StartDate}".
    /// Null when no date range filter is applied.
    /// </summary>
    public string? StartDate { get; }

    /// <summary>
    /// Gets the end date for the date range filter (inclusive).
    /// Supports parameter placeholders like "{EndDate}".
    /// Null when no date range filter is applied.
    /// </summary>
    public string? EndDate { get; }

    /// <summary>
    /// Gets the column name used for key-based filtering.
    /// Null when no key filter is applied.
    /// </summary>
    public string? KeyColumn { get; }

    /// <summary>
    /// Gets the value to match in the key filter (WHERE KeyColumn = Value).
    /// Supports parameter placeholders like "{CustomerId}".
    /// Null when no key filter is applied.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterConfig"/> class.
    /// </summary>
    /// <param name="dateColumn">
    /// The column name for date range filtering, or null for no date filter.
    /// </param>
    /// <param name="startDate">
    /// The start date (inclusive) for date range filtering, or null for no date filter.
    /// Supports parameter placeholders like "{StartDate}".
    /// </param>
    /// <param name="endDate">
    /// The end date (inclusive) for date range filtering, or null for no date filter.
    /// Supports parameter placeholders like "{EndDate}".
    /// </param>
    /// <param name="keyColumn">
    /// The column name for key-based filtering, or null for no key filter.
    /// </param>
    /// <param name="value">
    /// The value for key-based filtering, or null for no key filter.
    /// Supports parameter placeholders like "{CustomerId}".
    /// </param>
    /// <remarks>
    /// All parameters are optional (nullable). When specifying a date range filter,
    /// all three date parameters (dateColumn, startDate, endDate) should be provided.
    /// When specifying a key filter, both keyColumn and value should be provided.
    /// Parameter placeholder resolution occurs at runtime, not during configuration loading.
    /// </remarks>
    public FilterConfig(
        string? dateColumn = null,
        string? startDate  = null,
        string? endDate    = null,
        string? keyColumn  = null,
        string? value      = null)
    {
        DateColumn = dateColumn;
        StartDate = startDate;
        EndDate = endDate;
        KeyColumn = keyColumn;
        Value = value;
    }
}