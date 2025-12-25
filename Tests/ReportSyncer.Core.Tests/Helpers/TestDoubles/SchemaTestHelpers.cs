using System;
using System.Linq;
using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Tests.Helpers.TestDoubles;

public static class SchemaTestHelpers
{
    public static SchemaSnapshot CreateSnapshot(SchemaRole role, SchemaInspectionLevel level, params TableIdentifier[] tables)
    {
        var tableSchemas = tables.Select(t => new TableSchema(t, Array.Empty<ColumnSchema>(), null, null)).ToArray();
        return new SchemaSnapshot(tableSchemas, role, level);
    }
}
