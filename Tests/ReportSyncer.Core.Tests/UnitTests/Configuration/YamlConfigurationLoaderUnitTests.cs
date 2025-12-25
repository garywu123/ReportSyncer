using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Tests.Helpers;

namespace ReportSyncer.Core.Tests.UnitTests.Configuration;

[Trait("Type", "UnitTest")][Trait("Area", "Configuration")]
public class YamlConfigurationLoaderUnitTests
{
    // -----------------
    // Helper-based (fast) unit tests
    // -----------------
    [Fact]
    public void LoadFromHelper_WithValidMinimalConfig_ReturnsExpectedSyncConfiguration()
    {
        var cfg = ConfigurationYamlTestHelper.LoadFromYamlFile("sync_valid_minimal.yaml");
        Assert.NotNull(cfg);
        Assert.Equal("1.0", cfg.Version);
        Assert.NotEmpty(cfg.Connections);
        Assert.NotEmpty(cfg.SyncJobs);
    }

    [Fact]
    public void LoadFromHelper_WithFullExampleConfig_ReturnsExpectedSyncConfiguration()
    {
        var cfg = ConfigurationYamlTestHelper.LoadFromYamlFile("sync_valid_full.yaml");
        Assert.NotNull(cfg);
        Assert.Equal("1.1", cfg.Version);
        Assert.NotEmpty(cfg.Connections);
        Assert.True(cfg.SyncJobs.Count >= 1);
    }

    [Fact]
    public void LoadFromHelper_WithUnknownTopLevelKey_ThrowsConfigurationException()
    {
        Assert.Throws<ConfigurationException>(() =>
            ConfigurationYamlTestHelper.LoadFromYamlFile("sync_invalid_unknown_key.yaml")
        );
    }

    [Fact]
    public void LoadFromHelper_WithWrongPrimitiveType_ThrowsConfigurationException()
    {
        Assert.Throws<ConfigurationException>(() =>
            ConfigurationYamlTestHelper.LoadFromYamlFile("sync_invalid_wrong_primitive_type.yaml")
        );
    }

    [Fact]
    public void LoadFromHelper_WithInvalidSchemaPolicyValue_ThrowsConfigurationException()
    {
        Assert.Throws<ConfigurationException>(() =>
            ConfigurationYamlTestHelper.LoadFromYamlFile("sync_invalid_schema_policy_value.yaml")
        );
    }

    [Fact]
    public void LoadFromHelper_WithInvalidYamlSyntax_ThrowsConfigurationException()
    {
        Assert.Throws<ConfigurationException>(() =>
            ConfigurationYamlTestHelper.LoadFromYamlFile("sync_invalid_syntax.yaml")
        );
    }

    [Fact]
    public void LoadFromHelper_WithEmptyYaml_ThrowsConfigurationException()
    {
        Assert.Throws<ConfigurationException>(() =>
            ConfigurationYamlTestHelper.LoadFromYamlFile("sync_empty.yaml")
        );
    }

    // -----------------
    // File I/O + async integration-like unit tests (kept here for convenience)
    // -----------------
    private static string GetTestFilePath(string fileName)
    {
        var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
        return Path.Combine(baseDir, "TestFiles", fileName);
    }

    [Fact]
    public async Task LoadAsync_WithValidMinimalConfig_ReturnsExpectedSyncConfiguration_Async()
    {
        var path = GetTestFilePath("sync_valid_minimal.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        var cfg = await loader.LoadAsync(path, CancellationToken.None);

        Assert.NotNull(cfg);
        Assert.Equal("1.0", cfg.Version);
        Assert.True(cfg.Run.DryRun);
        Assert.Equal(2, cfg.Connections.Count);
        Assert.Single(cfg.SyncJobs);
        var job = cfg.SyncJobs[0];
        Assert.Equal("Job1", job.Name);
        Assert.Single(job.Tables);
        var table = job.Tables[0];
        Assert.Equal("dbo.SourceTable", table.Source);
        Assert.Equal("dbo.TargetTable", table.Target);
    }

    [Fact]
    public async Task LoadAsync_WithFullExampleConfig_ReturnsExpectedSyncConfiguration_Async()
    {
        var path = GetTestFilePath("sync_valid_full.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        var cfg = await loader.LoadAsync(path, CancellationToken.None);

        Assert.NotNull(cfg);
        Assert.Equal("1.2", cfg.Version);
        Assert.False(cfg.Run.DryRun);
        Assert.Equal(2, cfg.Connections.Count);
        Assert.Single(cfg.SyncJobs);
        var job = cfg.SyncJobs[0];
        Assert.Equal("FullJob", job.Name);
        Assert.Equal(2, job.Tables.Count);
        var orders = job.Tables[0];
        Assert.Equal("dbo.Orders", orders.Source);
        Assert.Equal("dbo.Orders_Tgt", orders.Target);
        Assert.True(orders.EnableIdentityInsert);
        Assert.NotNull(orders.ColumnMapping);
        Assert.True(orders.ColumnMapping.AutomapByName);

        Assert.NotNull(orders.ColumnMapping.Mappings);
        Assert.Equal("OrderId", orders.ColumnMapping.Mappings["OrderId"].FromSource);
        Assert.Equal("CustomerId", orders.ColumnMapping.Mappings["CustomerRef"].FromSource);
        Assert.Equal("ERP", orders.ColumnMapping.Mappings["SourceSystem"].Const);
        Assert.Equal("CurrentTimestamp", orders.ColumnMapping.Mappings["SyncTimestamp"].FromParameter);
        Assert.True(job.Parameters.ContainsKey("CurrentTimestamp"));
        Assert.False(string.IsNullOrWhiteSpace(job.Parameters["CurrentTimestamp"]));
        Assert.Equal("FULLSYNC", orders.ColumnMapping.Mappings["ExportTag"].Const);
        Assert.Equal("CustomerId", orders.ColumnMapping.Mappings["ParamValueCol"].FromParameter);
        Assert.True(orders.ColumnMapping.Mappings["IgnoredCol"].Ignore);
    }

    [Fact]
    public async Task LoadAsync_WithUnknownTopLevelKey_ThrowsConfigurationException_Async()
    {
        var path = GetTestFilePath("sync_invalid_unknown_key.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await loader.LoadAsync(path, CancellationToken.None);
        });
    }

    [Fact]
    public async Task LoadAsync_WithWrongPrimitiveType_ThrowsConfigurationException_Async()
    {
        var path = GetTestFilePath("sync_invalid_wrong_primitive_type.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await loader.LoadAsync(path, CancellationToken.None);
        });
    }

    [Fact]
    public async Task LoadAsync_WithInvalidSchemaPolicyValue_ThrowsConfigurationException_Async()
    {
        var path = GetTestFilePath("sync_invalid_schema_policy_value.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await loader.LoadAsync(path, CancellationToken.None);
        });
    }

    [Fact]
    public async Task LoadAsync_WithConfigFileContainingBadYaml_ThrowsConfigurationException_Async()
    {
        var path = GetTestFilePath("sync_invalid_syntax.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await loader.LoadAsync(path, CancellationToken.None);
        });
    }

    [Fact]
    public async Task LoadAsync_WithNonExistingFilePath_ThrowsFileNotFoundException_Async()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nonexistent_config_{Guid.NewGuid():N}.yaml");
        Assert.False(File.Exists(path), $"Test file unexpectedly exists: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await loader.LoadAsync(path, CancellationToken.None);
        });
    }

    [Fact]
    public async Task LoadAsync_WithParameterPlaceholders_ProducesExpectedModel_Async()
    {
        var path = GetTestFilePath("sync_with_placeholders.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        var cfg = await loader.LoadAsync(path, CancellationToken.None);

        Assert.NotNull(cfg);
        var job = cfg.SyncJobs.SingleOrDefault(j => j.Name == "JobWithPlaceholders");
        Assert.NotNull(job);
        Assert.True(job.Parameters.ContainsKey("StartDate"));
        Assert.Equal("{StartDate}", job.Parameters["StartDate"]);
        Assert.True(job.Parameters.ContainsKey("CustomerId"));
        Assert.Equal("{CustomerId}", job.Parameters["CustomerId"]);

        var table = job.Tables[0];
        Assert.NotNull(table.Filter);
        Assert.Equal("{StartDate}", table.Filter!.StartDate);
        Assert.Equal("{EndDate}", table.Filter!.EndDate);
    }

    [Fact]
    public async Task LoadAsync_WithExplicitMappingsOnly_PreservesColumnMappingConfig_Async()
    {
        var path = GetTestFilePath("sync_explicitMappings_only.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        var cfg = await loader.LoadAsync(path, CancellationToken.None);

        Assert.NotNull(cfg);
        var job = cfg.SyncJobs.Single(j => j.Name == "ExplicitMappingsOnlyJob");
        Assert.NotNull(job);
        Assert.Equal(2, job.Tables.Count);

        var productsTable = job.Tables[0];
        Assert.Equal("dbo.Products", productsTable.Source);
        Assert.Equal("dbo.Products_Target", productsTable.Target);
        Assert.NotNull(productsTable.ColumnMapping);
        Assert.False(productsTable.ColumnMapping.AutomapByName);
        Assert.NotNull(productsTable.ColumnMapping.Mappings);
        Assert.Equal(3, productsTable.ColumnMapping.Mappings.Count);
        Assert.Equal("ProductKey", productsTable.ColumnMapping.Mappings["ProductId"].FromSource);
        Assert.Equal("Name", productsTable.ColumnMapping.Mappings["ProductName"].FromSource);
        Assert.Equal("UnitPrice", productsTable.ColumnMapping.Mappings["Price"].FromSource);
        Assert.DoesNotContain(productsTable.ColumnMapping.Mappings, kv => !string.IsNullOrWhiteSpace(kv.Value.Const) || !string.IsNullOrWhiteSpace(kv.Value.FromParameter));

        var categoriesTable = job.Tables[1];
        Assert.Equal("dbo.Categories", categoriesTable.Source);
        Assert.Equal("dbo.Categories_Target", categoriesTable.Target);
        Assert.NotNull(categoriesTable.ColumnMapping);
        Assert.True(categoriesTable.ColumnMapping.AutomapByName);
        Assert.NotNull(categoriesTable.ColumnMapping.Mappings);
        Assert.Equal(2, categoriesTable.ColumnMapping.Mappings.Count);
        Assert.Equal("CatId", categoriesTable.ColumnMapping.Mappings["CategoryId"].FromSource);
        Assert.Equal("CatName", categoriesTable.ColumnMapping.Mappings["CategoryName"].FromSource);
        Assert.DoesNotContain(categoriesTable.ColumnMapping.Mappings, kv => !string.IsNullOrWhiteSpace(kv.Value.Const) || !string.IsNullOrWhiteSpace(kv.Value.FromParameter));
    }
}
