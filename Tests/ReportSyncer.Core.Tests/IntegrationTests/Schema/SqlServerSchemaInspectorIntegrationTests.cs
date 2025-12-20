using DotNetToolkit.Database.Abstractions;
using FluentAssertions;
using ReportSyncer.Core.Configuration;
using System.Threading;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Tests.IntegrationTests.Schema;

[Collection("SchemaIntegrationTests")]
[Trait("Type", "IntegrationTest")]
[Trait("Area", "Schema")]
public class SqlServerSchemaInspectorIntegrationTests(SchemaIntegrationDatabaseFixture fixture)
{
    private void CheckSkip()
    {
        // Use SkippableFact's Skip helper to skip tests when fixture indicates a reason
        Skip.If(!string.IsNullOrEmpty(fixture.SkipReason), fixture.SkipReason);
    }

    [SkippableFact]
    public async Task InspectAsync_WithEmptyTables_ReturnsEmptySnapshotWithSourceRole()
    {
        CheckSkip();
        var factory   = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var reqEmpty = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = Array.Empty<TableIdentifier>(),
            Role = SchemaRole.Source,
            Level = SchemaInspectionLevel.ExistenceOnly
        };

        var snapshot = await inspector.InspectAsync(reqEmpty, CancellationToken.None);

        snapshot.Role.Should().Be(reqEmpty.Role);
        snapshot.Tables.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task InspectAsync_WithEmptyTables_PreservesGivenRole_Target()
    {
        CheckSkip();
        var factory = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var reqTarget = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = Array.Empty<TableIdentifier>(),
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.ExistenceOnly
        };

        var snapshot = await inspector.InspectAsync(reqTarget, CancellationToken.None);

        snapshot.Role.Should().Be(SchemaRole.Target);
        snapshot.Tables.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task InspectAsync_WithSingleTable_ReturnsColumnsPkAndTypes()
    {
        CheckSkip();
        var factory   = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var tables   = new[] { new TableIdentifier("rs_test", "Customers") };
        var req = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = tables,
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.Full
        };

        var snapshot = await inspector.InspectAsync(req, CancellationToken.None);

        snapshot.Tables.Should().ContainSingle();
        var t = snapshot.Tables.Single();
        t.Table.TableName.Should().Be("Customers");
        t.Columns.Should()
           .Contain(c
                => string.Equals(c.Name, "Id", StringComparison.OrdinalIgnoreCase)
             && c.IsIdentity
             && c.IsPrimaryKeyPart
            );

        t.Columns.Should()
           .Contain(c
                => string.Equals(c.Name, "Name", StringComparison.OrdinalIgnoreCase)
             && !c.IsNullable
             && c.MaxLength == 50
            );

        t.Columns.Should()
           .Contain(c
                => string.Equals(c.Name, "Notes", StringComparison.OrdinalIgnoreCase)
             && c.IsNullable
            );
    }

    [SkippableFact]
    public async Task InspectAsync_ExistenceOnly_WithExistingTables_ReturnsMinimalSchemas()
    {
        CheckSkip();
        var factory = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var tables = new[] { new TableIdentifier("rs_test", "Customers") };
        var req = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = tables,
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.ExistenceOnly
        };

        var snapshot = await inspector.InspectAsync(req, CancellationToken.None);

        snapshot.Level.Should().Be(SchemaInspectionLevel.ExistenceOnly);
        snapshot.Tables.Should().NotBeEmpty();
        var ts = snapshot.Tables.Single();
        ts.Columns.Should().BeEmpty();
        ts.PrimaryKeyColumns.Should().BeEmpty();
        ts.ForeignKeys.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task InspectAsync_ExistenceOnly_MissingTable_Throws_ForBothRoles()
    {
        CheckSkip();
        var factory = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var missing = new[] { new TableIdentifier("rs_test", "NoSuchTable") };

        foreach (var role in new[] { SchemaRole.Source, SchemaRole.Target })
        {
            var req = new SchemaInspectionRequest
            {
                Connection = connConfig,
                Tables = missing,
                Role = role,
                Level = SchemaInspectionLevel.ExistenceOnly
            };

            Func<Task> act = async () => await inspector.InspectAsync(req, CancellationToken.None);

            await act.Should()
               .ThrowAsync<SchemaMismatchException>()
               .Where(e => e.Message.Contains("NoSuchTable"));
        }
    }

    [SkippableFact]
    public async Task InspectAsync_Full_WithSourceRole_ReturnsFullMetadata()
    {
        CheckSkip();
        var factory = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var tables = new[] { new TableIdentifier("rs_test", "Customers") };
        var req = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = tables,
            Role = SchemaRole.Source,
            Level = SchemaInspectionLevel.Full
        };

        var snapshot = await inspector.InspectAsync(req, CancellationToken.None);

        snapshot.Level.Should().Be(SchemaInspectionLevel.Full);
        snapshot.Tables.Should().ContainSingle();
        var ts = snapshot.Tables.Single();
        ts.Columns.Should().NotBeEmpty();
        ts.PrimaryKeyColumns.Should().NotBeEmpty();
    }

    [SkippableFact]
    public async Task InspectAsync_RespectsCancellationToken()
    {
        CheckSkip();
        var factory = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var tables = new[] { new TableIdentifier("rs_test", "Customers") };
        var req = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = tables,
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.Full
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = async () => await inspector.InspectAsync(req, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [SkippableFact]
    public async Task InspectAsync_WithForeignKeyCascade_ReturnsFkWithPairsAndCascadeFlag()
    {
        CheckSkip();

        var factory   = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var tables = new[]
        {
            new TableIdentifier("rs_test", "Customers"),
            new TableIdentifier("rs_test", "Orders")
        };

        var req = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = tables,
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.Full
        };

        var snapshot = await inspector.InspectAsync(req, CancellationToken.None);

        var orders = snapshot.Tables.Single(t => string.Equals(
                t.Table.TableName, "Orders", StringComparison.OrdinalIgnoreCase
            )
        );

        orders.ForeignKeys.Should().ContainSingle();
        var fk = orders.ForeignKeys.Single();
        fk.IsCascadeDelete.Should().BeTrue();
        fk.ColumnPairs.Should().ContainSingle();
        fk.ColumnPairs.Single().FromColumn.Should().Be("CustomerId");
    }

    [SkippableFact]
    public async Task InspectAsync_WithMultiColumnForeignKey_ReturnsOrderedPairs()
    {
        CheckSkip();

        var factory   = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var tables = new[]
        {
            new TableIdentifier("rs_test", "Parents"),
            new TableIdentifier("rs_test", "Children")
        };

        var req = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = tables,
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.Full
        };

        var snapshot = await inspector.InspectAsync(req, CancellationToken.None);

        var children = snapshot.Tables.Single(t => string.Equals(
                t.Table.TableName, "Children", StringComparison.OrdinalIgnoreCase
            )
        );

        var fk = children.ForeignKeys.Single();
        fk.ColumnPairs.Should().HaveCount(2);
        fk.ColumnPairs[0].FromColumn.Should().Be("ParentId");
        fk.ColumnPairs[1].FromColumn.Should().Be("RegionId");
    }

    [SkippableFact]
    public async Task InspectAsync_WithMissingTable_ThrowsSchemaMismatchException()
    {
        CheckSkip();
        var factory   = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var tables = new[] { new TableIdentifier("rs_test", "Nope") };

        var reqMissing = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = tables,
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.Full
        };

        Func<Task> act = async () => await inspector.InspectAsync(reqMissing, CancellationToken.None);

        await act.Should()
           .ThrowAsync<SchemaMismatchException>()
           .Where(e => e.Message.Contains("Nope"));
    }

    [SkippableFact]
    public async Task InspectAsync_WithUnknownType_MapsToObject()
    {
        CheckSkip();
        var factory   = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var tables   = new[] { new TableIdentifier("rs_test", "WeirdTypes") };
        var reqWeird = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = tables,
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.Full
        };

        var snapshot = await inspector.InspectAsync(reqWeird, CancellationToken.None);

        var t = snapshot.Tables.Single();
        var col = t.Columns.Single(c => c.Name.Equals("Payload", StringComparison.OrdinalIgnoreCase)
        );

        col.ClrType.Should().Be(typeof(object));
    }

    [SkippableFact]
    public async Task InspectAsync_OrderLines_ReturnsBothForeignKeys_WhenOnlyChildRequested()
    {
        CheckSkip();
        var factory   = fixture.CreateDbContextFactory();
        var inspector = new SqlServerSchemaInspector(factory);

        var connConfig = new ConnectionConfig(
            "test", fixture.ConnectionString!, EnvironmentType.Dev, ConnectionType.Application
        );

        var tables   = new[] { new TableIdentifier("rs_test", "OrderLines") };
        var reqOL = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = tables,
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.Full
        };

        var snapshot = await inspector.InspectAsync(reqOL, CancellationToken.None);

        var ol = snapshot.Tables.Single(t => string.Equals(
                t.Table.TableName, "OrderLines", StringComparison.OrdinalIgnoreCase
            )
        );

        // Should contain FK to Orders (OrderId) and FK to Products (ProductSku)
        ol.ForeignKeys.SelectMany(fk => fk.ColumnPairs)
           .Select(p => p.FromColumn)
           .Should()
           .Contain(["OrderId", "ProductSku"]);
    }

    [SkippableFact]
    public async Task InspectAsync_WithBadConnection_WrapsInSyncExecutionException()
    {
        CheckSkip();
        Func<object, IDbContext> badFactory =
            _ => throw new InvalidOperationException("Cannot connect");

        var inspector = new SqlServerSchemaInspector(badFactory);

        var connConfig = new ConnectionConfig(
            "test", "Server=bad;Database=bad;", EnvironmentType.Dev, ConnectionType.Application
        );

        var reqBad = new SchemaInspectionRequest
        {
            Connection = connConfig,
            Tables = new[] { new TableIdentifier("rs_test", "test") },
            Role = SchemaRole.Target,
            Level = SchemaInspectionLevel.Full
        };

        Func<Task> act = async () => await inspector.InspectAsync(reqBad, CancellationToken.None);

        await act.Should().ThrowAsync<SyncExecutionException>();
    }
}
