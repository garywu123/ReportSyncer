using System;
using System.Collections.Generic;
using DotNetToolkit.General;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;

namespace ReportSyncer.Core.Tests.Helpers.TestDoubles;

public class FakeResolver : IDependencyResolver
{
    public List<(SchemaSnapshot target, IReadOnlyList<TableIdentifier> tables)> Captured { get; } = new();
    public Exception? ExceptionToThrow { get; set; }
    public Func<SchemaSnapshot, IReadOnlyList<TableIdentifier>, Result<ExecutionPlan>>? OnBuild { get; set; }

    public Result<ExecutionPlan> BuildExecutionPlan(SchemaSnapshot targetSnapshot, IReadOnlyList<TableIdentifier> selectedTargetTables)
    {
        Captured.Add((targetSnapshot, selectedTargetTables));
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        if (OnBuild != null) return OnBuild(targetSnapshot, selectedTargetTables);
        return Result<ExecutionPlan>.Ok(ExecutionPlan.Empty);
    }
}
