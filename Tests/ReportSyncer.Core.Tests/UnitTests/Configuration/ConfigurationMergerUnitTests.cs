using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Tests.Helpers;

namespace ReportSyncer.Core.Tests.UnitTests.Configuration;

[Trait("Type", "UnitTest")]
[Trait("Area", "Configuration")]
public class ConfigurationMergerUnitTests
{
    [Fact]
    public void ApplyOverrides_NullOverrides_ReturnsSameInstance()
    {
        var baseCfg = ConfigurationTestData.CreateMinimalValidConfig();

        var effective = ConfigurationMerger.ApplyOverrides(baseCfg, overrides: null);

        Assert.Same(baseCfg, effective);
    }

    [Fact]
    public void ApplyOverrides_EmptyOverrides_ReturnsSameInstance()
    {
        var baseCfg = ConfigurationTestData.CreateMinimalValidConfig();
        var overrides = new RuntimeOverrides();

        var effective = ConfigurationMerger.ApplyOverrides(baseCfg, overrides);

        Assert.Same(baseCfg, effective);
    }

    [Fact]
    public void ApplyOverrides_DryRunOverride_ReplacesOnlyDryRun()
    {
        var baseCfg = ConfigurationTestData.CreateMinimalValidConfig();
        Assert.True(baseCfg.Run.DryRun); // sanity

        var overrides = new RuntimeOverrides { DryRun = false };

        var effective = ConfigurationMerger.ApplyOverrides(baseCfg, overrides);

        Assert.NotSame(baseCfg, effective);
        Assert.False(effective.Run.DryRun);
        // other run values preserved
        Assert.Equal(baseCfg.Run.DefaultBatchSize, effective.Run.DefaultBatchSize);
        Assert.Equal(baseCfg.Run.DeleteChunkSize, effective.Run.DeleteChunkSize);
        Assert.Equal(baseCfg.Run.UseTvpIfAvailable, effective.Run.UseTvpIfAvailable);
        Assert.Equal(baseCfg.Run.EtaSmoothing, effective.Run.EtaSmoothing);

        // original base unchanged
        Assert.True(baseCfg.Run.DryRun);
    }

    [Fact]
    public void ApplyOverrides_BatchSizeOverride_ReplacesDefaultBatchSizeOnly()
    {
        var baseCfg = ConfigurationTestData.CreateMinimalValidConfig();
        var overrides = new RuntimeOverrides { DefaultBatchSize = 2000 };

        var effective = ConfigurationMerger.ApplyOverrides(baseCfg, overrides);

        Assert.NotSame(baseCfg, effective);
        Assert.Equal(2000, effective.Run.DefaultBatchSize);
        // ensure other fields unchanged
        Assert.Equal(baseCfg.Run.DryRun, effective.Run.DryRun);
        Assert.Equal(baseCfg.Run.DeleteChunkSize, effective.Run.DeleteChunkSize);
    }

    [Fact]
    public void ApplyOverrides_InvalidBatchSize_ThrowsConfigurationException()
    {
        var baseCfg = ConfigurationTestData.CreateMinimalValidConfig();
        var overrides = new RuntimeOverrides { DefaultBatchSize = 0 };

        var ex = Assert.Throws<ConfigurationException>(() => ConfigurationMerger.ApplyOverrides(baseCfg, overrides));
        Assert.NotNull(ex.Errors);
        Assert.Contains(ex.Errors, e => e.Code == "CFG_BATCH_SIZE_INVALID");
    }

    [Fact]
    public void ApplyOverrides_MultipleOverrides_ProducesExpectedRunConfig()
    {
        var baseCfg = ConfigurationTestData.CreateMinimalValidConfig();
        var overrides = new RuntimeOverrides
        {
            DryRun = false,
            DefaultBatchSize = 1500,
            DeleteChunkSize = 2500,
            UseTvpIfAvailable = false,
            EtaSmoothing = 0.2
        };

        var effective = ConfigurationMerger.ApplyOverrides(baseCfg, overrides);

        Assert.False(effective.Run.DryRun);
        Assert.Equal(1500, effective.Run.DefaultBatchSize);
        Assert.Equal(2500, effective.Run.DeleteChunkSize);
        Assert.False(effective.Run.UseTvpIfAvailable);
        Assert.Equal(0.2, effective.Run.EtaSmoothing);
    }
}
