// File: ReportSyncer.Core/Schema/Dependency/DependencyResolver.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System;
using System.Collections.Generic;
using System.Linq;
using DotNetToolkit.General;
using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Schema.Dependency;

/// <summary>
/// Default implementation of <see cref="IDependencyResolver"/> using topological sorting.
/// </summary>
public sealed class DependencyResolver : IDependencyResolver
{
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
            var cycle = visiting.Concat(new[] { node }).ToList();
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
