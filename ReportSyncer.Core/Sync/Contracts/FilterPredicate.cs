// ============================================================================
// File: FilterPredicate.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Filter contract for query builders.
// ============================================================================

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Represents a single filter condition applied to table operations.
/// </summary>
/// <param name="ColumnName">Column to filter on.</param>
/// <param name="Operator">Comparison operator to use.</param>
/// <param name="Value">Primary comparison value.</param>
/// <param name="Value2">
/// Secondary comparison value required for <see cref="FilterOperator.BetweenInclusive"/>.
/// Ignored for other operators.
/// </param>
public sealed record FilterPredicate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FilterPredicate"/> record.
    /// </summary>
    /// <param name="columnName">Column to filter on.</param>
    /// <param name="operator">Comparison operator to use.</param>
    /// <param name="value">Primary comparison value.</param>
    /// <param name="value2">Secondary comparison value required for <see cref="FilterOperator.BetweenInclusive"/>.</param>
    public FilterPredicate(string columnName, FilterOperator @operator, object? value, object? value2 = null)
    {
        ArgumentNullException.ThrowIfNull(columnName);
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be empty or whitespace.", nameof(columnName));

        if (@operator == FilterOperator.BetweenInclusive && value2 is null)
            throw new ArgumentException("Value2 is required for BetweenInclusive filters.", nameof(value2));

        ColumnName = columnName;
        Operator = @operator;
        Value = value;
        Value2 = value2;
    }

    /// <summary>Column to filter on.</summary>
    public string ColumnName { get; }

    /// <summary>Comparison operator to use.</summary>
    public FilterOperator Operator { get; }

    /// <summary>Primary comparison value.</summary>
    public object? Value { get; }

    /// <summary>
    /// Secondary comparison value required for <see cref="FilterOperator.BetweenInclusive"/>.
    /// Ignored for other operators.
    /// </summary>
    public object? Value2 { get; }
}

/// <summary>
/// Supported filter operators.
/// </summary>
public enum FilterOperator
{
    /// <summary>Equality comparison.</summary>
    Equals,

    /// <summary>Greater than or equal comparison.</summary>
    GreaterOrEqual,

    /// <summary>Less than or equal comparison.</summary>
    LessOrEqual,

    /// <summary>Inclusive between comparison (requires two values).</summary>
    BetweenInclusive
}
