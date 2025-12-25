using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Tests.Helpers.TestDoubles;

public class RecordingFakeInspector : ISchemaInspector
{
    public List<SchemaInspectionRequest> Requests { get; } = new();

    public int CallCount => Requests.Count;

    public Task<SchemaSnapshot> InspectAsync(SchemaInspectionRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        var empty = new SchemaSnapshot(Enumerable.Empty<TableSchema>(), request.Role, request.Level);
        return Task.FromResult(empty);
    }
}
