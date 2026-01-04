// ============================================================================
// File: RunOptionsResolverTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Unit tests for run options resolution with priority merging.
// ============================================================================

using ReportSyncer.Console.Hosting;

namespace ReportSyncer.Console.Tests.Hosting;

public sealed class RunOptionsResolverTests
{
    [Fact]
    public void Resolve_CliOverridesAppsettings_ForConfigPath()
    {
        // Arrange
        var cli = new CliRunOptions("cli.yaml", false);
        var appSettings = new HostAppSettings(new HostRunSettings("app.yaml", true));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("cli.yaml", options.JobConfigPath);
        Assert.False(options.DryRunOverride); // CLI wins
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_AppsettingsOverridesDefaults_ForConfigPath()
    {
        // Arrange
        var cli = new CliRunOptions(null, null);
        var appSettings = new HostAppSettings(new HostRunSettings("app.yaml", true));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("app.yaml", options.JobConfigPath);
        Assert.True(options.DryRunOverride);
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_DefaultsWhenNeitherProvided()
    {
        // Arrange
        var cli = new CliRunOptions(null, null);
        var appSettings = new HostAppSettings(new HostRunSettings(null, null));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal(HostDefaults.DefaultJobConfigPath, options.JobConfigPath);
        Assert.Null(options.DryRunOverride); // No override
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_CliDryRunOverridesAppsettings()
    {
        // Arrange
        var cli = new CliRunOptions(null, false);
        var appSettings = new HostAppSettings(new HostRunSettings(null, true));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.False(options.DryRunOverride); // CLI wins
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_AppsettingsDryRunWhenCliNotProvided()
    {
        // Arrange
        var cli = new CliRunOptions(null, null);
        var appSettings = new HostAppSettings(new HostRunSettings(null, true));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.True(options.DryRunOverride);
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_NoDryRunOverrideWhenNeitherProvided()
    {
        // Arrange
        var cli = new CliRunOptions(null, null);
        var appSettings = new HostAppSettings(new HostRunSettings(null, null));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Null(options.DryRunOverride); // No override means Core uses its default
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_NullAppsettingsRun_UsesDefaults()
    {
        // Arrange
        var cli = new CliRunOptions(null, null);
        var appSettings = new HostAppSettings(null);

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal(HostDefaults.DefaultJobConfigPath, options.JobConfigPath);
        Assert.Null(options.DryRunOverride);
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_CliConfigOnly_AppsettingsDryRun()
    {
        // Arrange
        var cli = new CliRunOptions("cli.yaml", null);
        var appSettings = new HostAppSettings(new HostRunSettings("app.yaml", false));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("cli.yaml", options.JobConfigPath); // CLI wins
        Assert.False(options.DryRunOverride); // appsettings wins (no CLI override)
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_CliDryRunOnly_AppsettingsConfig()
    {
        // Arrange
        var cli = new CliRunOptions(null, true);
        var appSettings = new HostAppSettings(new HostRunSettings("app.yaml", false));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("app.yaml", options.JobConfigPath); // appsettings wins (no CLI override)
        Assert.True(options.DryRunOverride); // CLI wins
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_WhitespaceCliConfig_TreatedAsNotProvided()
    {
        // Arrange
        var cli = new CliRunOptions("   ", null);
        var appSettings = new HostAppSettings(new HostRunSettings("app.yaml", null));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal("app.yaml", options.JobConfigPath); // whitespace CLI ignored, appsettings wins
        Assert.Null(error);
    }

    [Fact]
    public void Resolve_WhitespaceAppsettingsConfig_UsesDefaults()
    {
        // Arrange
        var cli = new CliRunOptions(null, null);
        var appSettings = new HostAppSettings(new HostRunSettings("   ", null));

        // Act
        var (ok, options, error) = RunOptionsResolver.Resolve(cli, appSettings);

        // Assert
        Assert.True(ok);
        Assert.NotNull(options);
        Assert.Equal(HostDefaults.DefaultJobConfigPath, options.JobConfigPath); // whitespace appsettings ignored, defaults win
        Assert.Null(error);
    }
}
