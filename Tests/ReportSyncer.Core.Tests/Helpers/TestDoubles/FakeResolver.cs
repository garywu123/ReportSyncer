using System;
using System.Collections.Generic;
using DotNetToolkit.General;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;

namespace ReportSyncer.Core.Tests.Helpers.TestDoubles;

public class FakeResolver : IDependencyResolver
{
    public List<(SchemaSnapshot target, IReadOnlyList<TableIdentifier> tables, IReadOnlyDictionary<TableIdentifier, bool> ignoreMap)> Captured { get; } = new();
    public Exception? ExceptionToThrow { get; set; }
    public Func<SchemaSnapshot, IReadOnlyList<TableIdentifier>, IReadOnlyDictionary<TableIdentifier, bool>, Result<ExecutionPlan>>? OnBuild { get; set; }

    public Result<ExecutionPlan> BuildExecutionPlan(
        SchemaSnapshot targetSnapshot,
        IReadOnlyList<TableIdentifier> selectedTargetTables,
        IReadOnlyDictionary<TableIdentifier, bool> ignoreDependenciesMap)
    {
        Captured.Add((targetSnapshot, selectedTargetTables, ignoreDependenciesMap));
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        if (OnBuild != null) return OnBuild(targetSnapshot, selectedTargetTables, ignoreDependenciesMap);
        return Result<ExecutionPlan>.Ok(ExecutionPlan.Empty);
    }
}
