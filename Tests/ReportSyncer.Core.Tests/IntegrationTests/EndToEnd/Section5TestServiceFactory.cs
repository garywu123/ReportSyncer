using DotNetToolkit.Database.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Schema.Services;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Sql;
using ReportSyncer.Core.Tests.Helpers;
using ReportSyncer.Core.Tests.Helpers.Observability;

namespace ReportSyncer.Core.Tests.IntegrationTests.EndToEnd;

internal static class Section5TestServiceFactory
{
    public static Section5TestServices Create(Section5EndToEndFixture fixture)
    {
        if (fixture is null) throw new ArgumentNullException(nameof(fixture));
        if (fixture.ConnectionString is null) throw new InvalidOperationException("Fixture not initialized.");

        var logger = NullLogger.Instance;
        var progress = new InMemoryProgressReporter();

        // Configuration pipeline
        var loader = new YamlConfigurationLoader(NullLogger<YamlConfigurationLoader>.Instance);
        var validator = new ConfigurationValidator();
        var configProvider = new ConfigurationProvider(loader, validator, NullLogger<ConfigurationProvider>.Instance);

        // Schema pipeline
        var inspector = new SqlServerSchemaInspector(fixture.CreateInspectorContextFactory());
        var mapper = new SchemaMapper();
        var resolver = new DependencyResolver();
        var schemaService = new SchemaService(inspector, mapper, resolver);
        var preflight = new PreFlightValidator(schemaService, NullLogger<PreFlightValidator>.Instance);

        // Execution pipeline
        var connectionFactory = fixture.CreateConnectionFactory();
        var dbContextFactory = fixture.CreateDbContextFactory();
        var sqlBuilder = new SqlServerQueryBuilder();
        var identityManager = new IdentityInsertManager(dbContextFactory);
        var writer = new SqlDataWriter(dbContextFactory, sqlBuilder, identityManager);
        var tableRunner = new TableRunner(connectionFactory, writer, sqlBuilder, progress);

        var orchestrator = new SyncOrchestrator(configProvider, preflight, tableRunner, progress);
        return new Section5TestServices(orchestrator, progress);
    }
}

internal sealed record Section5TestServices(ISyncOrchestrator Orchestrator, InMemoryProgressReporter Progress);
