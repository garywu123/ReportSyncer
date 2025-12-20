using System.Collections.Generic;
using ReportSyncer.Core.Configuration;

namespace ReportSyncer.Core.Schema;

/// <summary>
/// Request object for schema inspection.
/// </summary>
public sealed class SchemaInspectionRequest
{
    public required ConnectionConfig Connection { get; init; }

    public required IReadOnlyCollection<TableIdentifier> Tables { get; init; }

    public required SchemaRole Role { get; init; }

    public required SchemaInspectionLevel Level { get; init; }
}
