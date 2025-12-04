using System;
using System.Collections.Generic;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Tests.Testing;
using Xunit;

namespace ReportSyncer.Core.Tests.Configuration;

/// <summary>
/// Unit tests for ConfigurationValidator (per Task2.3_TestPlan.md).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
public class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator = new();

    [Fact]
    public void Phase0_InfrastructureIsWired()
    {
        Assert.NotNull(_validator);
    }

    [Fact]
    public void Phase0_ConfigurationTestDataFactoryWorks()
    {
        var config = ConfigurationTestData.CreateMinimalValidConfig();
        Assert.NotNull(config);
        Assert.NotEmpty(config.Version);
        Assert.NotEmpty(config.Connections);
        Assert.NotEmpty(config.SyncJobs);
    }

    [Fact]
    public void Validate_MinimalValidConfiguration_DoesNotThrow()
    {
        var config = ConfigurationTestData.CreateMinimalValidConfig();

        var ex = Record.Exception(() => _validator.Validate(config));

        Assert.Null(ex);
    }

    [Fact]
    public void Validate_FullValidConfiguration_DoesNotThrow()
    {
        var cfg = ConfigurationTestData.CreateFullValidConfig();

        var ex = Record.Exception(() => _validator.Validate(cfg));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_WithNullConfiguration_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() => _validator.Validate(null!));
    }

    [Fact]
    public void Validate_MissingRunSettings_ThrowsConfigurationException_WithRunMissingError()
    {
        var cfg = ConfigurationTestData.CreateConfigWithNullRun();

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        Assert.Contains(ex.Errors, e => e.Code == "CFG_RUN_MISSING");
    }

    [Fact]
    public void Validate_MissingSafetySettings_ThrowsConfigurationException_WithSafetyMissingError()
    {
        var cfg = ConfigurationTestData.CreateConfigWithNullSafety();

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        Assert.Contains(ex.Errors, e => e.Code == "CFG_SAFETY_MISSING");
    }

    [Fact]
    public void Validate_EmptyConnectionsCollection_ThrowsConfigurationException_WithNoConnectionsError()
    {
        var cfg = ConfigurationTestData.CreateConfigWithEmptyConnections();

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        Assert.Contains(ex.Errors, e => e.Code == "CFG_CONNECTIONS_EMPTY");
    }

    [Fact]
    public void Validate_EmptyJobsCollection_ThrowsConfigurationException_WithNoJobsError()
    {
        var cfg = ConfigurationTestData.CreateConfigWithEmptyJobs();

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        Assert.Contains(ex.Errors, e => e.Code == "CFG_JOBS_EMPTY");
    }

    [Fact]
    public void Validate_WithNullConnectionsCollection_ReportsNullConnectionsError()
    {
        var cfg = ConfigurationTestData.CreateConfigWithNullConnections();

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        // Validator treats null or empty connections the same and reports 'CFG_CONNECTIONS_EMPTY'
        Assert.Contains(ex.Errors, e => e.Code == "CFG_CONNECTIONS_EMPTY");
    }

    [Fact]
    public void Validate_WithNullJobsCollection_ReportsNullJobsError()
    {
        var cfg = ConfigurationTestData.CreateConfigWithNullJobs();

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        // Validator treats null or empty jobs the same and reports 'CFG_JOBS_EMPTY'
        Assert.Contains(ex.Errors, e => e.Code == "CFG_JOBS_EMPTY");
    }

    [Fact]
    public void Validate_WithConnectionMissingName_ReportsConnectionNameMissing()
    {
        var minimal = ConfigurationTestData.CreateMinimalValidConfig();
        var run = minimal.Run;
        var safety = minimal.Safety;
        var schema = minimal.SchemaPolicy;

        var badConn = ConfigurationTestData.CreateConnectionWithRaw("", "Server=.;Database=Bad;Integrated Security=true;", EnvironmentType.Dev, ConnectionType.Application);
        var goodConn = new ConnectionConfig("ExistingConn", "Server=.;Database=Good;Integrated Security=true;", EnvironmentType.Dev, ConnectionType.Application);

        var table = minimal.SyncJobs[0].Tables[0];
        var job = new SyncJobConfig("JobA", null, goodConn.Name, goodConn.Name, new Dictionary<string, string>(), new[] { table });

        var cfg = new SyncConfiguration("1.0", run, safety, schema, new[] { badConn, goodConn }, new[] { job });

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        Assert.Contains(ex.Errors, e => e.Code == "CFG_CONNECTION_NAME_MISSING");
    }

    [Fact]
    public void Validate_WithDuplicateConnectionNames_ReportsDuplicateConnectionName()
    {
        var minimal = ConfigurationTestData.CreateMinimalValidConfig();
        var run = minimal.Run;
        var safety = minimal.Safety;
        var schema = minimal.SchemaPolicy;

        var c1 = new ConnectionConfig("DupConn", "Server=.;Database=D1;Integrated Security=true;", EnvironmentType.Dev, ConnectionType.Application);
        var c2 = new ConnectionConfig("DupConn", "Server=.;Database=D2;Integrated Security=true;", EnvironmentType.Dev, ConnectionType.Reporting);

        var table = minimal.SyncJobs[0].Tables[0];
        var job = new SyncJobConfig("JobB", null, c1.Name, c1.Name, new Dictionary<string, string>(), new[] { table });

        var cfg = new SyncConfiguration("1.0", run, safety, schema, new[] { c1, c2 }, new[] { job });

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        Assert.Contains(ex.Errors, e => e.Code == "CFG_CONNECTION_NAME_DUPLICATE");
    }

    [Fact]
    public void Validate_WithInvalidEnvironmentType_ReportsInvalidConnectionEnvironment()
    {
        var minimal = ConfigurationTestData.CreateMinimalValidConfig();
        var run = minimal.Run;
        var safety = minimal.Safety;
        var schema = minimal.SchemaPolicy;

        // Create a connection with an invalid enum value for Environment
        var badEnv = (EnvironmentType)999;
        var badConn = ConfigurationTestData.CreateConnectionWithRaw("BadEnvConn", "Server=.;Database=BadEnv;Integrated Security=true;", badEnv, ConnectionType.Application);

        var table = minimal.SyncJobs[0].Tables[0];
        var job = new SyncJobConfig("JobEnv", null, badConn.Name, badConn.Name, new Dictionary<string, string>(), new[] { table });

        var cfg = new SyncConfiguration("1.0", run, safety, schema, new[] { badConn }, new[] { job });

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        Assert.Contains(ex.Errors, e => e.Code == "CFG_CONNECTION_ENV_INVALID");
    }

    [Fact]
    public void Validate_WithInvalidConnectionType_ReportsInvalidConnectionType()
    {
        var minimal = ConfigurationTestData.CreateMinimalValidConfig();
        var run = minimal.Run;
        var safety = minimal.Safety;
        var schema = minimal.SchemaPolicy;

        // Create a connection with an invalid enum value for Type
        var badType = (ConnectionType)999;
        var badConn = ConfigurationTestData.CreateConnectionWithRaw("BadTypeConn", "Server=.;Database=BadType;Integrated Security=true;", EnvironmentType.Dev, badType);

        var table = minimal.SyncJobs[0].Tables[0];
        var job = new SyncJobConfig("JobType", null, badConn.Name, badConn.Name, new Dictionary<string, string>(), new[] { table });

        var cfg = new SyncConfiguration("1.0", run, safety, schema, new[] { badConn }, new[] { job });

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        Assert.Contains(ex.Errors, e => e.Code == "CFG_CONNECTION_TYPE_INVALID");
    }

    [Fact]
    public void Validate_WithMissingConnectionString_ReportsConnectionStringMissing()
    {
        var minimal = ConfigurationTestData.CreateMinimalValidConfig();
        var run = minimal.Run;
        var safety = minimal.Safety;
        var schema = minimal.SchemaPolicy;

        // Create a connection with an empty connection string
        var badConn = ConfigurationTestData.CreateConnectionWithRaw("NoConnStr", "", EnvironmentType.Dev, ConnectionType.Application);

        var table = minimal.SyncJobs[0].Tables[0];
        var job = new SyncJobConfig("JobConnStr", null, badConn.Name, badConn.Name, new Dictionary<string, string>(), new[] { table });

        var cfg = new SyncConfiguration("1.0", run, safety, schema, new[] { badConn }, new[] { job });

        var ex = Assert.Throws<ConfigurationException>(() => _validator.Validate(cfg));

        Assert.Contains(ex.Errors, e => e.Code == "CFG_CONNECTION_STRING_MISSING");
    }
}
