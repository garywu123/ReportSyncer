using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Tests.Testing;
using Xunit;

namespace ReportSyncer.Core.Tests.Configuration;

[Trait("Category","Integration")]
public class YamlConfigurationLoaderIntegrationTests
{
    private static string GetTestFilePath(string fileName)
    {
        var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
        return Path.Combine(baseDir, "Configuration", "TestFiles", fileName);
    }

    [Fact]
    public async Task LoadAsync_WithValidMinimalConfigFile_LoadsSuccessfully()
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
    }

    [Fact]
    public async Task LoadAsync_WithValidFullConfigFile_LoadsAllSections()
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
    }

    [Fact]
    public async Task LoadAsync_WithConfigFileContainingUnknownKey_ThrowsConfigurationException()
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
    public async Task LoadAsync_WithConfigFileContainingInvalidPolicy_ThrowsConfigurationException()
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
        var path = Path.Combine(Path.GetTempPath(), $"nonexistent_config_integration_{Guid.NewGuid():N}.yaml");
        Assert.False(File.Exists(path), $"Test file unexpectedly exists: {path}");

        var loader = new YamlConfigurationLoader(new DummyLogService());
        await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            await loader.LoadAsync(path, CancellationToken.None);
        });
    }
}
