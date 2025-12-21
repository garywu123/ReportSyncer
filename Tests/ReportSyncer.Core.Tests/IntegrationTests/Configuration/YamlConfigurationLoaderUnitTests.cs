using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Tests.Helpers;

namespace ReportSyncer.Core.Tests.IntegrationTests.Configuration;

[Trait("Type", "IntegrationTest")]
[Trait("Area", "Configuration")]
public class YamlConfigurationLoaderUnitTests
{
    private static string GetTestFilePath(string fileName)
    {
        // Test files are copied to output by the csproj; use BaseDirectory to locate them.
        var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
        return Path.Combine(baseDir, "TestFiles", fileName);
    }

    

    [Fact]
    public async Task LoadAsync_WithValidMinimalConfig_ReturnsExpectedSyncConfiguration()
    {
        var path = GetTestFilePath("sync_valid_minimal.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        var cfg    = await loader.LoadAsync(path, CancellationToken.None);

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
    public async Task LoadAsync_WithFullExampleConfig_ReturnsExpectedSyncConfiguration()
    {
        var path = GetTestFilePath("sync_valid_full.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        var cfg    = await loader.LoadAsync(path, CancellationToken.None);

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
    }

    [Fact]
    public async Task LoadAsync_WithUnknownTopLevelKey_ThrowsConfigurationException()
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
    public async Task LoadAsync_WithUnknownNestedKey_ThrowsConfigurationException()
    {
        var path = GetTestFilePath("sync_invalid_unknown_nested_key.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await loader.LoadAsync(path, CancellationToken.None);
        });
    }

    [Fact]
    public async Task LoadAsync_WithWrongPrimitiveType_ThrowsConfigurationException()
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
    public async Task LoadAsync_WithInvalidSchemaPolicyValue_ThrowsConfigurationException()
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
    public async Task LoadAsync_WithConfigFileContainingInvalidPolicy_ThrowsConfigurationException()
    {
        // Re-use the schema-policy invalid fixture to represent an invalid policy value at top-level
        var path = GetTestFilePath("sync_invalid_schema_policy_value.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await loader.LoadAsync(path, CancellationToken.None);
        });
    }

    [Fact]
    public async Task LoadAsync_WithConfigFileContainingBadYaml_ThrowsConfigurationException()
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
    public async Task LoadAsync_WithNonExistingFilePath_ThrowsConfigurationException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nonexistent_config_{Guid.NewGuid():N}.yaml");
        Assert.False(File.Exists(path), $"Test file unexpectedly exists: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await loader.LoadAsync(path, CancellationToken.None);
        });
    }

    [Fact]
    public async Task LoadAsync_WithParameterPlaceholders_ProducesExpectedModel()
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
    public async Task LoadAsync_WithExplicitMappingsOnly_PreservesColumnMappingConfig()
    {
        // Arrange
        var path = GetTestFilePath("sync_explicitMappings_only.yaml");
        Assert.True(File.Exists(path), $"Test YAML not found: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());

        // Act
        var cfg = await loader.LoadAsync(path, CancellationToken.None);

        // Assert
        Assert.NotNull(cfg);
        var job = cfg.SyncJobs.Single(j => j.Name == "ExplicitMappingsOnlyJob");
        Assert.NotNull(job);
        Assert.Equal(2, job.Tables.Count);

        // Verify first table: automapByName=false + explicitMappings (no addedColumns)
        var productsTable = job.Tables[0];
        Assert.Equal("dbo.Products", productsTable.Source);
        Assert.Equal("dbo.Products_Target", productsTable.Target);
        Assert.NotNull(productsTable.ColumnMapping);
        Assert.False(productsTable.ColumnMapping.AutomapByName);
        Assert.NotNull(productsTable.ColumnMapping.ExplicitMappings);
        Assert.Equal(3, productsTable.ColumnMapping.ExplicitMappings.Count);
        Assert.Equal("ProductKey", productsTable.ColumnMapping.ExplicitMappings["ProductId"]);
        Assert.Equal("Name", productsTable.ColumnMapping.ExplicitMappings["ProductName"]);
        Assert.Equal("UnitPrice", productsTable.ColumnMapping.ExplicitMappings["Price"]);
        Assert.NotNull(productsTable.ColumnMapping.AddedColumns);
        Assert.Empty(productsTable.ColumnMapping.AddedColumns);

        // Verify second table: automapByName=true + explicitMappings (no addedColumns)
        var categoriesTable = job.Tables[1];
        Assert.Equal("dbo.Categories", categoriesTable.Source);
        Assert.Equal("dbo.Categories_Target", categoriesTable.Target);
        Assert.NotNull(categoriesTable.ColumnMapping);
        Assert.True(categoriesTable.ColumnMapping.AutomapByName);
        Assert.NotNull(categoriesTable.ColumnMapping.ExplicitMappings);
        Assert.Equal(2, categoriesTable.ColumnMapping.ExplicitMappings.Count);
        Assert.Equal("CatId", categoriesTable.ColumnMapping.ExplicitMappings["CategoryId"]);
        Assert.Equal("CatName", categoriesTable.ColumnMapping.ExplicitMappings["CategoryName"]);
        Assert.NotNull(categoriesTable.ColumnMapping.AddedColumns);
        Assert.Empty(categoriesTable.ColumnMapping.AddedColumns);
    }
}