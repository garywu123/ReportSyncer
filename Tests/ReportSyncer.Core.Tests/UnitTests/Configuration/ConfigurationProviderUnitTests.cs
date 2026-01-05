using Microsoft.Extensions.Logging.Abstractions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Tests.Helpers;

namespace ReportSyncer.Core.Tests.UnitTests.Configuration;

[Trait("Type", "UnitTest")][Trait("Area", "Configuration")]
public class ConfigurationProviderUnitTests
{
    [Fact]
    public async Task LoadAndValidateAsync_WithoutOverrides_CallsLoaderAndValidatorOnce_ReturnsBaseConfig()
    {
        var baseCfg = ConfigurationTestData.CreateMinimalValidConfig();
        var loader = new FakeLoader(baseCfg);
        var validator = new FakeValidator();
        var logger = NullLogger<ConfigurationProvider>.Instance;

        var provider = new ConfigurationProvider(loader, validator, logger);

        var result = await provider.LoadAndValidateAsync("some-path.yaml", CancellationToken.None);

        Assert.Same(baseCfg, result);
        Assert.True(loader.LoadCalled);
        Assert.True(validator.ValidateCalled);
        Assert.Same(baseCfg, validator.LastValidated);
    }

    [Fact]
    public async Task LoadAndValidateAsync_WithOverrides_CallsLoaderAndValidatorOnce_AppliesOverrides()
    {
        var baseCfg = ConfigurationTestData.CreateMinimalValidConfig();
        var loader = new FakeLoader(baseCfg);
        var validator = new FakeValidator();
        var logger = NullLogger<ConfigurationProvider>.Instance;

        var provider = new ConfigurationProvider(loader, validator, logger);

        var overrides = new RuntimeOverrides { DryRun = false, DefaultBatchSize = 2000 };

        var result = await provider.LoadAndValidateAsync("some-path.yaml", overrides, CancellationToken.None);

        Assert.NotSame(baseCfg, result);
        Assert.False(result.Run.DryRun);
        Assert.Equal(2000, result.Run.DefaultBatchSize);
        Assert.True(loader.LoadCalled);
        Assert.True(validator.ValidateCalled);
    }

    [Fact]
    public async Task LoadAndValidateAsync_WithInvalidOverrides_ThrowsConfigurationException_FromMerger()
    {
        var baseCfg = ConfigurationTestData.CreateMinimalValidConfig();
        var loader = new FakeLoader(baseCfg);
        var validator = new FakeValidator();
        var logger = NullLogger<ConfigurationProvider>.Instance;

        var provider = new ConfigurationProvider(loader, validator, logger);

        var overrides = new RuntimeOverrides { DefaultBatchSize = 0 };

        await Assert.ThrowsAsync<ConfigurationException>(
            () => provider.LoadAndValidateAsync("some-path.yaml", overrides, CancellationToken.None)
        );

        Assert.True(loader.LoadCalled);
        Assert.True(validator.ValidateCalled);
    }

    // --- Test fakes ---
    private class FakeLoader : IConfigurationLoader
    {
        private readonly SyncConfiguration _cfg;
        public bool LoadCalled { get; private set; }

        public FakeLoader(SyncConfiguration cfg) => _cfg = cfg;

        public Task<SyncConfiguration> LoadAsync(string path, CancellationToken ct)
        {
            LoadCalled = true;
            return Task.FromResult(_cfg);
        }
    }

    private class FakeValidator : IConfigurationValidator
    {
        public bool ValidateCalled { get; private set; }
        public SyncConfiguration? LastValidated { get; private set; }

        public void Validate(SyncConfiguration configuration)
        {
            ValidateCalled = true;
            LastValidated = configuration;
        }
    }
}
