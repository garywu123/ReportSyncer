using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ReportSyncer.Core.Schema;

/// <summary>
/// Compatibility shim to forward old InspectAsync(connection, tables) calls to the new request-based API.
/// This is an incremental aid and preserves the previous convention: empty table list -> Source role, otherwise Target.
/// </summary>
public static class SchemaInspectorCompatibilityExtensions
{
    public static Task<SchemaSnapshot> InspectAsync(this ISchemaInspector inspector,
        ReportSyncer.Core.Configuration.ConnectionConfig connection,
        IEnumerable<TableIdentifier> tables,
        CancellationToken ct = default)
    {
        var list = (tables ?? Enumerable.Empty<TableIdentifier>()).ToList();
        var role = list.Count == 0 ? SchemaRole.Source : SchemaRole.Target;

        var request = new SchemaInspectionRequest
        {
            Connection = connection,
            Tables = list,
            Role = role,
            Level = SchemaInspectionLevel.Full
        };

        return inspector.InspectAsync(request, ct);
    }
}
