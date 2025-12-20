using FluentAssertions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Tests.Helpers;

#pragma warning disable CA1806
namespace ReportSyncer.Core.Tests.UnitTests.Schema;

[Trait("Type", "UnitTest")]
[Trait("Area", "Schema")]
public class SqlServerSchemaInspectorUnitTests
{
    private static ConnectionConfig MakeConn()
        => new(
            "C", "Server=.;Database=X;Trusted_Connection=true;", EnvironmentType.Dev,
            ConnectionType.Application
        );

    [Fact]
    public async Task InspectAsync_Throws_On_Null_Connection()
    {
        var inspector = new SqlServerSchemaInspector(_ => new FakeDbContext());
        Func<Task> act = async () => await inspector.InspectAsync(
            null!, new[] { new TableIdentifier("dbo", "T") }
        );

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InspectAsync_Throws_On_Null_Tables()
    {
        var        inspector = new SqlServerSchemaInspector(_ => new FakeDbContext());
        Func<Task> act       = async () => await inspector.InspectAsync(MakeConn(), null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InspectAsync_Returns_Empty_Snapshot_For_Empty_Request()
    {
        var inspector = new SqlServerSchemaInspector(_ => new FakeDbContext());
        var snap      = await inspector.InspectAsync(MakeConn(), Array.Empty<TableIdentifier>());
        snap.Tables.Should().BeEmpty();
        // If the table is empty, then the role would be the source.
        snap.Role.Should().Be(SchemaRole.Source);
    }

    [Fact]
    public async Task InspectAsync_Throws_SchemaMismatch_When_Table_Not_Found()
    {
        var fake = new FakeDbContext();
        // no columns for dbo.Missing
        var inspector = new SqlServerSchemaInspector(_ => fake);

        Func<Task> act = async () => await inspector.InspectAsync(
            MakeConn(), [new TableIdentifier("dbo", "Missing")]
        );

        await act.Should()
           .ThrowAsync<SchemaMismatchException>()
           .Where(e => e.Message.Contains("Missing"));
    }

    [Fact]
    public async Task InspectAsync_Maps_Columns_And_PkFlags()
    {
        var fake = new FakeDbContext();
        var key  = "dbo.T";
        fake.ColumnRows[key] =
        [
            new Dictionary<string, object>
            {
                ["SchemaName"] = "dbo",
                ["TableName"] = "T",
                ["ColumnName"] = "Id",
                ["DataType"] = "int",
                ["MaxLength"] = (int?)null ?? 0,
                ["IsNullable"] = false,
                ["IsIdentity"] = true,
                ["IsPrimaryKeyPart"] = true
            },
            new Dictionary<string, object>
            {
                ["SchemaName"] = "dbo",
                ["TableName"] = "T",
                ["ColumnName"] = "Name",
                ["DataType"] = "nvarchar",
                ["MaxLength"] = 100,
                ["IsNullable"] = true,
                ["IsIdentity"] = false,
                ["IsPrimaryKeyPart"] = false
            }
        ];

        // no FK rows
        var inspector = new SqlServerSchemaInspector(_ => fake);
        var snap = await inspector.InspectAsync(
            MakeConn(), [new TableIdentifier("dbo", "T")]
        );

        snap.Tables.Should().HaveCount(1);
        var ts = snap.Tables.First();
        ts.Columns.Should().HaveCount(2);
        var idCol = ts.GetColumn("id");
        idCol.Should().NotBeNull();
        idCol.ClrType.Should().Be(typeof(int));
        idCol.IsIdentity.Should().BeTrue();
        ts.PrimaryKeyColumns.Should().ContainSingle().Which.Should().Be("Id");
        var nameCol = ts.GetColumn("Name");
        nameCol?.MaxLength.Should().Be(100);
    }

    [Fact]
    public async Task InspectAsync_Groups_FKs_And_Preserves_Order_And_Cascade()
    {
        var fake = new FakeDbContext();
        var key  = "dbo.Child";
        fake.ColumnRows[key] =
        [
            new Dictionary<string, object>
            {
                ["SchemaName"] = "dbo",
                ["TableName"] = "Child",
                ["ColumnName"] = "C1",
                ["DataType"] = "int",
                ["MaxLength"] = (int?)null ?? 0,
                ["IsNullable"] = false,
                ["IsIdentity"] = false,
                ["IsPrimaryKeyPart"] = false
            }
        ];

        // two FK rows with same FK name mapping two columns
        fake.FkRows =
        [
            new Dictionary<string, object>
            {
                ["ForeignKeyName"] = "FK1",
                ["FromSchema"] = "dbo",
                ["FromTable"] = "Child",
                ["FromColumn"] = "C1",
                ["ToSchema"] = "dbo",
                ["ToTable"] = "Parent",
                ["ToColumn"] = "P1",
                ["DeleteAction"] = "CASCADE"
            },
            new Dictionary<string, object>
            {
                ["ForeignKeyName"] = "FK1",
                ["FromSchema"] = "dbo",
                ["FromTable"] = "Child",
                ["FromColumn"] = "C2",
                ["ToSchema"] = "dbo",
                ["ToTable"] = "Parent",
                ["ToColumn"] = "P2",
                ["DeleteAction"] = "CASCADE"
            }
        ];

        var inspector = new SqlServerSchemaInspector(_ => fake);
        var snap = await inspector.InspectAsync(
            MakeConn(), [new TableIdentifier("dbo", "Child")]
        );

        var ts = snap.Tables.Single();
        ts.ForeignKeys.Should().HaveCount(1);
        var fk = ts.ForeignKeys.Single();
        fk.IsCascadeDelete.Should().BeTrue();
        fk.ColumnPairs.Should().HaveCount(2);
        fk.ColumnPairs[0].FromColumn.Should().Be("C1");
        fk.ColumnPairs[1].FromColumn.Should().Be("C2");
    }

    [Fact]
    public async Task InspectAsync_Ignores_FKs_For_Unrequested_Tables()
    {
        var fake = new FakeDbContext();
        fake.ColumnRows["dbo.A"] =
        [
            new Dictionary<string, object>
            {
                ["SchemaName"] = "dbo",
                ["TableName"] = "A",
                ["ColumnName"] = "Id",
                ["DataType"] = "int",
                ["IsNullable"] = false,
                ["IsIdentity"] = false,
                ["IsPrimaryKeyPart"] = true
            }
        ];

        // FK from B -> C (neither requested)
        fake.FkRows =
        [
            new Dictionary<string, object>
            {
                ["ForeignKeyName"] = "FK_B_C",
                ["FromSchema"] = "dbo",
                ["FromTable"] = "B",
                ["FromColumn"] = "X",
                ["ToSchema"] = "dbo",
                ["ToTable"] = "C",
                ["ToColumn"] = "Y",
                ["DeleteAction"] = "NO_ACTION"
            }
        ];

        var inspector = new SqlServerSchemaInspector(_ => fake);
        var snap      = await inspector.InspectAsync(MakeConn(), [new TableIdentifier("dbo", "A")]);
        var ts        = snap.Tables.Single();
        ts.ForeignKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_Maps_Unknown_Type_To_Object()
    {
        var fake = new FakeDbContext();
        fake.ColumnRows["dbo.X"] =
        [
            new Dictionary<string, object>
            {
                ["SchemaName"] = "dbo",
                ["TableName"] = "X",
                ["ColumnName"] = "F",
                ["DataType"] = "weirdtype",
                ["MaxLength"] = (int ?) null ?? 0,
                ["IsNullable"] = true,
                ["IsIdentity"] = false,
                ["IsPrimaryKeyPart"] = false
            }
        ];

        var inspector = new SqlServerSchemaInspector(_ => fake);
        var snap      = await inspector.InspectAsync(MakeConn(), [new TableIdentifier("dbo", "X")]);
        var col       = snap.Tables.Single().GetColumn("F");
        col!.ClrType.Should().Be(typeof(object));
    }

    [Fact]
    public async Task InspectAsync_Wraps_Unexpected_Exception_In_SyncExecutionException()
    {
        var bad       = new BadDbContext();
        var inspector = new SqlServerSchemaInspector(_ => bad);
        Func<Task> act = async () => await inspector.InspectAsync(
            MakeConn(), [new TableIdentifier("dbo", "T")]
        );

        await act.Should()
           .ThrowAsync<SyncExecutionException>()
           .Where(e => e.Message.Contains("boom"));
    }
}
