// File: ReportSyncer.Core/Schema/Dependency/DependencyValidationError.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

namespace ReportSyncer.Core.Schema.Dependency;

/// <summary>
/// Represents a validation error encountered during dependency analysis.
/// </summary>
public sealed record DependencyValidationError
{
    /// <summary>
    /// Type of dependency error encountered.
    /// </summary>
    public DependencyErrorKind Kind { get; init; }

    /// <summary>
    /// Primary table involved in the error (e.g., the dependent table).
    /// </summary>
    public TableIdentifier? Table { get; init; }

    /// <summary>
    /// Missing or problematic referenced table (for <see cref="DependencyErrorKind.MissingParent"/>).
    /// </summary>
    public TableIdentifier? ReferencedTable { get; init; }

    /// <summary>
    /// Tables involved in a cycle (for <see cref="DependencyErrorKind.CycleDetected"/>).
    /// </summary>
    public IReadOnlyList<TableIdentifier>? CyclePath { get; init; }

    /// <summary>
    /// Human-readable description of the error.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Creates an error indicating a selected table depends on an unselected parent table.
    /// </summary>
    public static DependencyValidationError MissingParent(
        TableIdentifier dependentTable,
        TableIdentifier missingParent)
    {
        return new DependencyValidationError
        {
            Kind = DependencyErrorKind.MissingParent,
            Table = dependentTable,
            ReferencedTable = missingParent,
            Message = $"Table '{dependentTable}' has a foreign key to '{missingParent}', " +
                      $"but '{missingParent}' is not included in the selected tables for this job. " +
                      $"Either add '{missingParent}' to the job or remove '{dependentTable}'."
        };
    }

    /// <summary>
    /// Creates an error indicating a circular dependency was detected.
    /// </summary>
    public static DependencyValidationError Cycle(IReadOnlyList<TableIdentifier> cyclePath)
    {
        var pathStr = string.Join(" → ", cyclePath.Select(t => t.ToString()));
        return new DependencyValidationError
        {
            Kind = DependencyErrorKind.CycleDetected,
            CyclePath = cyclePath,
            Message = $"Circular dependency detected: {pathStr}. " +
                      $"Cannot determine a safe execution order."
        };
    }
}

/// <summary>
/// Categories of dependency validation errors.
/// </summary>
public enum DependencyErrorKind
{
    /// <summary>
    /// A selected table has a FK to a non-selected table.
    /// </summary>
    MissingParent,

    /// <summary>
    /// A circular FK relationship exists among selected tables.
    /// </summary>
    CycleDetected
}
