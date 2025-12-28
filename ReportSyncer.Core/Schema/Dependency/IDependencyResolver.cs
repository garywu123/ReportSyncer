// File: ReportSyncer.Core/Schema/Dependency/IDependencyResolver.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using DotNetToolkit.General;

namespace ReportSyncer.Core.Schema.Dependency;

/// <summary>
/// Resolves table dependencies and generates a safe execution order
/// for synchronization operations based on foreign key relationships.
/// </summary>
/// <remarks>
/// <para>
/// This resolver enforces two critical rules:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Dependency Closure:</b> If a selected target table has a FK to another
/// target table, that referenced table must also be selected (or the operation fails).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Acyclic Graph:</b> Circular FK relationships are not supported and cause failure.
/// </description>
/// </item>
/// </list>
/// <para>
/// The resolver only examines FK relationships in the <paramref name="targetSnapshot"/>;
/// source database FKs do not affect execution order.
/// </para>
/// </remarks>
public interface IDependencyResolver
{
    /// <summary>
    /// Builds an execution plan that respects FK dependencies among the selected target tables.
    /// </summary>
    /// <param name="targetSnapshot">
    /// The target database schema snapshot containing FK metadata.
    /// </param>
    /// <param name="selectedTargetTables">
    /// Tables selected for this sync job (from enabled <c>TableTask</c> entries).
    /// Must match tables present in <paramref name="targetSnapshot"/>.
    /// </param>
    /// <param name="ignoreDependenciesMap">
    /// Map of table identifiers to a flag indicating whether FK parent validation should be bypassed for that table.
    /// </param>
    /// <returns>
    /// Success: <see cref="ExecutionPlan"/> with topologically sorted insert/delete orders.
    /// Failure: Error message with details about missing parents or cycles.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="targetSnapshot"/> or <paramref name="selectedTargetTables"/> is null.
    /// </exception>
    /// <remarks>
    /// <para><b>Failure Scenarios:</b></para>
    /// <list type="bullet">
    /// <item>
    /// <description>A selected table has a FK to a non-selected target table.</description>
    /// </item>
    /// <item>
    /// <description>A cycle exists among selected tables (e.g., A → B → C → A).</description>
    /// </item>
    /// </list>
    /// <para><b>Success Behavior:</b></para>
    /// <list type="bullet">
    /// <item>
    /// <description>InsertOrder places parent tables before children.</description>
    /// </item>
    /// <item>
    /// <description>DeleteOrder is the reverse (children before parents).</description>
    /// </item>
    /// <item>
    /// <description>Tables with no FKs may appear in any consistent order.</description>
    /// </item>
    /// </list>
    /// </remarks>
    Result<ExecutionPlan> BuildExecutionPlan(
        SchemaSnapshot targetSnapshot,
        IReadOnlyList<TableIdentifier> selectedTargetTables,
        IReadOnlyDictionary<TableIdentifier, bool> ignoreDependenciesMap);
}
