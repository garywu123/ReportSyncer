// File: ReportSyncer.Core/Schema/Dependency/ExecutionPlan.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System;
using System.Collections.Generic;
using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Schema.Dependency;

/// <summary>
/// Represents a validated execution plan for table synchronization operations.
/// The plan defines the order in which tables must be processed to respect
/// foreign key relationships.
/// </summary>
/// <param name="InsertOrder">
/// Tables ordered for safe insertion: parent tables appear before child tables
/// that reference them.
/// </param>
/// <param name="DeleteOrder">
/// Tables ordered for safe deletion: child tables appear before parent tables
/// they reference. This is the reverse of <see cref="InsertOrder"/>.
/// </param>
/// <remarks>
/// <para>
/// An empty list in either order indicates no tables require ordering
/// (e.g., no tables selected or no FK relationships among selected tables).
/// </para>
/// <example>
/// Given FK: Orders → Customers
/// <code>
/// InsertOrder = [Customers, Orders]  // Insert Customers first
/// DeleteOrder = [Orders, Customers]  // Delete Orders first
/// </code>
/// </example>
/// </remarks>
public sealed record ExecutionPlan(
    IReadOnlyList<TableIdentifier> InsertOrder,
    IReadOnlyList<TableIdentifier> DeleteOrder)
{
    /// <summary>
    /// Creates an empty execution plan with no tables.
    /// </summary>
    public static ExecutionPlan Empty => new(
        Array.Empty<TableIdentifier>(),
        Array.Empty<TableIdentifier>()
    );
}
