using FluentAssertions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Schema.Services;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Tests.UnitTests.Sync;

public class PreFlightResultTests
{
    [Fact]
    public void Constructor_AssignsProperties()
    {
        // Arrange
        var schema = CreateSchemaAnalysisResult();

        // Act
        var result = new PreFlightResult("JobA", dryRun: true, schema);

        // Assert
        result.JobName.Should().Be("JobA");
        result.DryRun.Should().BeTrue();
        result.Schema.Should().BeSameAs(schema);
        result.Schema.ExecutionPlan.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullSchema_Throws()
    {
        // Act
        Action act = () => new PreFlightResult("JobA", false, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static SchemaAnalysisResult CreateSchemaAnalysisResult()
    {
        var tableId = TableIdentifier.Parse("dbo.Parent");
        var columns = new List<ColumnSchema>
        {
            new("Id", typeof(int), "int", isNullable: false, isIdentity: true, isPrimaryKeyPart: true, maxLength: null)
        };

        var tableSchema = new TableSchema(tableId, columns, new[] { "Id" }, Array.Empty<ForeignKeySchema>());
        var sourceSnapshot = new SchemaSnapshot(new[] { tableSchema }, SchemaRole.Source, SchemaInspectionLevel.Full);
        var targetSnapshot = new SchemaSnapshot(new[] { tableSchema }, SchemaRole.Target, SchemaInspectionLevel.Full);
        var mapping = new SchemaMappingResult(true, Array.Empty<SchemaMappingError>(), new Dictionary<TableIdentifier, TableMapping>());
        var plan = ExecutionPlan.Empty;

        return new SchemaAnalysisResult(sourceSnapshot, targetSnapshot, mapping, plan);
    }
}
