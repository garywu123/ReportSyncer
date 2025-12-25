using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetToolkit.General;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Mapping;

namespace ReportSyncer.Core.Tests.Helpers.TestDoubles;

public class FakeMapper : ISchemaMapper
{
    public List<(SchemaSnapshot source, SchemaSnapshot target, SyncJobConfig job)> Captured { get; } = new();
    public Exception? ExceptionToThrow { get; set; }
    public Func<SchemaSnapshot, SchemaSnapshot, SyncJobConfig, ConnectionConfig, ConnectionConfig, SchemaPolicyConfig, Result<SchemaMappingResult>>? OnMap { get; set; }

    public Result<SchemaMappingResult> MapJob(SchemaSnapshot sourceSnapshot, SchemaSnapshot targetSnapshot, SyncJobConfig job, ConnectionConfig sourceConnection, ConnectionConfig targetConnection, SchemaPolicyConfig schemaPolicy)
    {
        Captured.Add((sourceSnapshot, targetSnapshot, job));
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        if (OnMap != null) return OnMap(sourceSnapshot, targetSnapshot, job, sourceConnection, targetConnection, schemaPolicy);
        var empty = new SchemaMappingResult(true, Array.Empty<SchemaMappingError>(), new Dictionary<TableIdentifier, TableMapping>());
        return Result<SchemaMappingResult>.Ok(empty);
    }
}
