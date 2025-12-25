using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Tests.Helpers.TestDoubles;

public class FakeInspector : ISchemaInspector
{
    public List<SchemaInspectionRequest> CapturedRequests { get; } = new();
    public Exception? ExceptionToThrow { get; set; }
    public Func<SchemaInspectionRequest, CancellationToken, Task<SchemaSnapshot>>? OnInspect { get; set; }

    public async Task<SchemaSnapshot> InspectAsync(SchemaInspectionRequest request, CancellationToken ct = default)
    {
        CapturedRequests.Add(request);
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        if (OnInspect != null) return await OnInspect(request, ct).ConfigureAwait(false);
        return new SchemaSnapshot(Array.Empty<TableSchema>(), request.Role, request.Level);
    }
}
