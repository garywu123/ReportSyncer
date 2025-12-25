// File: ReportSyncer.Core/Schema/Dependency/DependencyResolver.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using DotNetToolkit.General;

namespace ReportSyncer.Core.Schema.Dependency;

/// <summary>
/// Default implementation of <see cref="IDependencyResolver"/> using topological sorting.
/// </summary>
public sealed class DependencyResolver : IDependencyResolver
{
    /// <summary>
    /// Build an <see cref="ExecutionPlan"/> for the provided <paramref name="selectedTargetTables"/> against
    /// the <paramref name="targetSnapshot"/>. Performs validation that all selected tables exist and that
    /// parent tables referenced by foreign keys are also selected. Uses a topological sort to derive a
    /// safe insert order; the delete order is the reverse of the insert order.
    /// </summary>
    /// <param name="targetSnapshot">The inspected target schema snapshot.</param>
    /// <param name="selectedTargetTables">The list of tables selected for the job (may be empty).</param>
    /// <returns>
    /// A <see cref="Result{ExecutionPlan}"/> containing the computed <see cref="ExecutionPlan"/> on success,
    /// or a failure result with an error message when validation or cycle detection fails.
    /// If <paramref name="selectedTargetTables"/> is empty the returned plan will be <see cref="ExecutionPlan.Empty"/>.
    /// </returns>
    public Result<ExecutionPlan> BuildExecutionPlan(
        SchemaSnapshot targetSnapshot,
        IReadOnlyList<TableIdentifier> selectedTargetTables)
    {
        Guard.NotNull(targetSnapshot, nameof(targetSnapshot));
        Guard.NotNull(selectedTargetTables, nameof(selectedTargetTables));

        if (selectedTargetTables.Count == 0)
        {
            return Result<ExecutionPlan>.Ok(ExecutionPlan.Empty);
        }

        var selectedSet = new HashSet<TableIdentifier>(selectedTargetTables);
        // adjList 代表有向图的邻接表
        var adjList = new Dictionary<TableIdentifier, List<TableIdentifier>>();
        var errors = new List<DependencyValidationError>();

        foreach (var table in selectedTargetTables)
        {
            if (!targetSnapshot.TryGetTable(table, out var tableSchema) || tableSchema is null)
            {
                return Result<ExecutionPlan>.Fail($"Table '{table}' not found in target snapshot.");
            }

            if (!adjList.ContainsKey(table))
            {
                adjList[table] = new List<TableIdentifier>();
            }

            // 遍历每个表的外键依赖
            foreach (var fk in tableSchema.ForeignKeys)
            {
                var parent = fk.ToTable;
                if (!selectedSet.Contains(parent))
                {
                    errors.Add(DependencyValidationError.MissingParent(table, parent));
                }
                else
                {
                    adjList[table].Add(parent);
                }
            }
        }

        if (errors.Count > 0)
        {
            var errorMsg = string.Join("; ", errors.Select(e => e.Message));
            return Result<ExecutionPlan>.Fail(errorMsg);
        }

        var sortResult = TopologicalSort(adjList, selectedSet);
        if (!sortResult.IsSuccess)
        {
            return Result<ExecutionPlan>.Fail(sortResult.Error!);
        }

        var insertOrder = sortResult.Value.ToArray();
        var deleteOrder = insertOrder.Reverse().ToArray();

        return Result<ExecutionPlan>.Ok(new ExecutionPlan(insertOrder, deleteOrder));
    }

    /// <summary>
    /// Perform a topological sort of the dependency graph represented by <paramref name="adjList"/>.
    /// The returned list is in dependency order such that parents appear before dependent children.
    /// </summary>
    /// <param name="adjList">Adjacency list mapping a node to its dependencies (edges point to parents).</param>
    /// <param name="allNodes">All nodes that must appear in the result (selected tables).</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the ordered node list on success, or a failure with an
    /// explanatory error (e.g. cycle detected) on failure.
    /// </returns>
    private Result<IReadOnlyList<TableIdentifier>> TopologicalSort(
        Dictionary<TableIdentifier, List<TableIdentifier>> adjList,
        HashSet<TableIdentifier> allNodes)
    {
        var visited = new HashSet<TableIdentifier>();
        var visiting = new HashSet<TableIdentifier>();
        var result = new List<TableIdentifier>();

        foreach (var node in allNodes)
        {
            if (!visited.Contains(node))
            {
                var visitResult = Visit(node, adjList, visited, visiting, result);
                if (!visitResult.IsSuccess)
                {
                    return Result<IReadOnlyList<TableIdentifier>>.Fail(visitResult.Error!);
                }
            }
        }

        return Result<IReadOnlyList<TableIdentifier>>.Ok(result);
    }

    /// <summary>
    /// Depth-first visitation helper used by <see cref="TopologicalSort"/>.
    /// Detects cycles and appends nodes to <paramref name="result"/> in post-order.
    /// </summary>
    /// <param name="node">The current node to visit.</param>
    /// <param name="adjList">Adjacency list of dependencies.</param>
    /// <param name="visited">Set of nodes already fully visited.</param>
    /// <param name="visiting">Set of nodes currently on the recursion stack (for cycle detection).</param>
    /// <param name="result">Accumulator for the topologically sorted nodes (post-order).</param>
    /// <returns>
    /// A <see cref="Result{bool}"/> indicating success, or failure with an error message when a cycle
    /// is detected or a recursive visit fails.
    /// </returns>
    private Result<bool> Visit(
        TableIdentifier node,
        Dictionary<TableIdentifier, List<TableIdentifier>> adjList,
        HashSet<TableIdentifier> visited,
        HashSet<TableIdentifier> visiting,
        List<TableIdentifier> result)
    {
        if (visiting.Contains(node))
        {
            // build cycle path for nicer message
            var cycle = visiting.Concat([node]).ToList();
            var err = DependencyValidationError.Cycle(cycle);
            return Result<bool>.Fail(err.Message);
        }

        if (visited.Contains(node))
        {
            return Result<bool>.Ok(true);
        }

        visiting.Add(node);

        if (adjList.TryGetValue(node, out var dependencies))
        {
            foreach (var dep in dependencies)
            {
                var visitResult = Visit(dep, adjList, visited, visiting, result);
                if (!visitResult.IsSuccess)
                {
                    return visitResult;
                }
            }
        }

        visiting.Remove(node);
        visited.Add(node);
        result.Add(node);

        return Result<bool>.Ok(true);
    }
}
