using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using ReportSyncer.Core.Configuration;

namespace ReportSyncer.Core.Tests.Configuration;

/// <summary>
/// Factory for valid test configurations ("golden" configs mutated for negative tests).
/// </summary>
public static class ConfigurationTestData
{
    /// <summary>
    /// Creates a minimal valid config: one connection, one job, one table.
    /// </summary>
    public static SyncConfiguration CreateMinimalValidConfig()
    {
        var connection = new ConnectionConfig(
            name: "TestConnection_Dev",
            connectionString: "Server=.;Database=TestDb;Integrated Security=true;",
            environment: EnvironmentType.Dev,
            type: ConnectionType.Application
        );

        var table = new TableTaskConfig(
            source: "dbo.SourceTable",
            target: "dbo.TargetTable",
            enabled: true
        );

        var job = new SyncJobConfig(
            name: "TestJob1",
            description: "Minimal test sync job",
            sourceConnection: "TestConnection_Dev",
            targetConnection: "TestConnection_Dev",
            parameters: new Dictionary<string, string>(),
            tables: new[] { table }
        );

        var runConfig = new RunConfig(
            dryRun: true,
            defaultBatchSize: 1000,
            deleteChunkSize: 500,
            useTvpIfAvailable: true,
            etaSmoothing: 0.5
        );

        var safetyConfig = new SafetyConfig(
            forbidProdToProd: true,
            requireDifferentConnections: false,
            confirmLargeDeletePct: 0.1
        );

        var schemaPolicyConfig = new SchemaPolicyConfig(
            onMismatch: SchemaMismatchBehavior.Warn,
            requirePrimaryKey: true,
            allowExtraTargetColumns: false
        );

        return new SyncConfiguration(
            version: "1.0",
            run: runConfig,
            safety: safetyConfig,
            schemaPolicy: schemaPolicyConfig,
            connections: new[] { connection },
            syncJobs: new[] { job }
        );
    }

        /// <summary>
        /// Create a ConnectionConfig instance without invoking its constructor so tests can simulate invalid values.
        /// </summary>
        public static ConnectionConfig CreateConnectionWithRaw(string? name, string connectionString, EnvironmentType environment, ConnectionType type)
        {
            // Create a valid instance first (using a harmless temporary connection string),
            // then overwrite backing fields (including readonly backing fields) via reflection.
            var tempName = "__TMP_CONN__" + Guid.NewGuid().ToString("N");
            var tempConnStr = "Server=.;Database=__TMP__";
            var obj = new ConnectionConfig(tempName, tempConnStr, environment, type);
            // Auto-property backing fields use the pattern <PropertyName>k__BackingField
            SetPrivateField(obj, "<Name>k__BackingField", name);
            SetPrivateField(obj, "<ConnectionString>k__BackingField", connectionString);
            SetPrivateField(obj, "<Environment>k__BackingField", environment);
            SetPrivateField(obj, "<Type>k__BackingField", type);
            return obj;
        }

    /// <summary>
    /// Creates a richer configuration with multiple connections, jobs, and tables.
    /// </summary>
    public static SyncConfiguration CreateFullValidConfig()
    {
        var minimal = CreateMinimalValidConfig();
        var sourceConnection = minimal.Connections[0];

        var stagingConnection = new ConnectionConfig(
            name: "TgtDB_Staging",
            connectionString: "Server=stg;Database=Tgt;Integrated Security=true;",
            environment: EnvironmentType.Staging,
            type: ConnectionType.Reporting
        );

        var connections = new List<ConnectionConfig>
        {
            sourceConnection,
            stagingConnection
        };

        var ordersTable = new TableTaskConfig(
            source: "dbo.Orders",
            target: "dbo.Orders_Tgt",
            enabled: true,
            preSyncTargetAction: false,
            allowAllDelete: false,
            enableIdentityInsert: true,
            filter: new FilterConfig(
                dateColumn: "OrderDate",
                startDate: "2025-01-01",
                endDate: "2025-12-31"
            ),
            columnMapping: new ColumnMappingConfig(
                automapByName: true,
                explicitMappings: new Dictionary<string, string>
                {
                    ["OrderId"] = "OrderId",
                    ["CustomerRef"] = "CustomerId"
                }
            ),
            keys: new KeyConfig(new[] { "OrderId" }),
            syncOptions: new SyncOptionsConfig(batchSize: 250, useTvp: true)
        );

        var job = new SyncJobConfig(
            name: "FullJob",
            description: "Full featured job",
            sourceConnection: sourceConnection.Name,
            targetConnection: stagingConnection.Name,
            parameters: new Dictionary<string, string>
            {
                ["CustomerId"] = "42",
                ["StartDate"] = "2025-01-01"
            },
            tables: new[] { minimal.SyncJobs[0].Tables[0], ordersTable }
        );

        var jobs = new List<SyncJobConfig>
        {
            minimal.SyncJobs[0],
            job
        };

        var runConfig = new RunConfig(
            dryRun: false,
            defaultBatchSize: 500,
            deleteChunkSize: 200,
            useTvpIfAvailable: true,
            etaSmoothing: 0.05
        );

        var safetyConfig = new SafetyConfig(
            forbidProdToProd: true,
            requireDifferentConnections: true,
            confirmLargeDeletePct: 0.2
        );

        var schemaPolicyConfig = new SchemaPolicyConfig(
            onMismatch: SchemaMismatchBehavior.Fail,
            requirePrimaryKey: true,
            allowExtraTargetColumns: true
        );

        return new SyncConfiguration(
            version: "1.1",
            run: runConfig,
            safety: safetyConfig,
            schemaPolicy: schemaPolicyConfig,
            connections: connections,
            syncJobs: jobs
        );
    }

    /// <summary>
    /// Returns a copy of the minimal valid config but with the private Run backing field set to null
    /// to simulate a configuration missing the Run section for validator testing.
    /// </summary>
    public static SyncConfiguration CreateConfigWithNullRun()
    {
        var cfg = CreateMinimalValidConfig();
        SetPrivateField(cfg, "<Run>k__BackingField", null);
        return cfg;
    }

    /// <summary>
    /// Returns a copy of the minimal valid config but with the private Safety backing field set to null
    /// to simulate a configuration missing the Safety section for validator testing.
    /// </summary>
    public static SyncConfiguration CreateConfigWithNullSafety()
    {
        var cfg = CreateMinimalValidConfig();
        SetPrivateField(cfg, "<Safety>k__BackingField", null);
        return cfg;
    }

    /// <summary>
    /// Returns a copy of the minimal valid config but with an empty connections array.
    /// </summary>
    public static SyncConfiguration CreateConfigWithEmptyConnections()
    {
        var cfg = CreateMinimalValidConfig();
        // _connections is the private readonly backing field for Connections
        SetPrivateField(cfg, "_connections", System.Array.Empty<ConnectionConfig>());
        return cfg;
    }

    /// <summary>
    /// Returns a copy of the minimal valid config but with an empty syncJobs array.
    /// </summary>
    public static SyncConfiguration CreateConfigWithEmptyJobs()
    {
        var cfg = CreateMinimalValidConfig();
        // _syncJobs is the private readonly backing field for SyncJobs
        SetPrivateField(cfg, "_syncJobs", System.Array.Empty<SyncJobConfig>());
        return cfg;
    }

    /// <summary>
    /// Returns a copy of the minimal valid config but with the private connections backing field set to null
    /// to simulate a configuration with a null connections collection for validator testing.
    /// </summary>
    public static SyncConfiguration CreateConfigWithNullConnections()
    {
        var cfg = CreateMinimalValidConfig();
        SetPrivateField(cfg, "_connections", null);
        return cfg;
    }

    /// <summary>
    /// Returns a copy of the minimal valid config but with the private syncJobs backing field set to null
    /// to simulate a configuration with a null jobs collection for validator testing.
    /// </summary>
    public static SyncConfiguration CreateConfigWithNullJobs()
    {
        var cfg = CreateMinimalValidConfig();
        SetPrivateField(cfg, "_syncJobs", null);
        return cfg;
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi == null)
            throw new InvalidOperationException($"Private field '{fieldName}' not found on type '{target.GetType()}'");

        fi.SetValue(target, value);
    }
}
