// ============================================================================
// File: ConfigurationProvider.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-06
// Description: Implementation of IConfigurationProvider that loads, validates, and applies runtime overrides.
// ============================================================================

using Microsoft.Extensions.Logging;

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Default implementation of <see cref="IConfigurationProvider"/>.
/// </summary>
/// <remarks>
/// This provider is the canonical entry point for producing the "effective" configuration
/// used by the rest of the system. Hosts SHOULD call `LoadAndValidateAsync(path, overrides, ct)` to
/// receive a single immutable <see cref="SyncConfiguration"/> that already incorporates any
/// permitted runtime overrides. Components downstream (for example, <c>PreFlightValidator</c>,
/// <c>SyncOrchestrator</c>, data writers) MUST accept only <see cref="SyncConfiguration"/> and
/// MUST NOT accept or apply <see cref="RuntimeOverrides"/> directly.
/// 
/// This centralization keeps override validation and safety checks in one place and prevents
/// inconsistent behavior caused by ad-hoc overrides in multiple components.
/// </remarks>
public class ConfigurationProvider : IConfigurationProvider
{
    private readonly IConfigurationLoader _loader;
    private readonly IConfigurationValidator _validator;
    private readonly ILogger<ConfigurationProvider> _logger;

    /// <summary>
    /// Constructs a new <see cref="ConfigurationProvider"/>.
    /// </summary>
    public ConfigurationProvider(
        IConfigurationLoader loader,
        IConfigurationValidator validator,
        ILogger<ConfigurationProvider> logger)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<SyncConfiguration> LoadAndValidateAsync(string path, CancellationToken ct)
    {
        return LoadAndValidateAsync(path, overrides: null, ct);
    }

    /// <inheritdoc />
    public async Task<SyncConfiguration> LoadAndValidateAsync(string path, RuntimeOverrides? overrides, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // 1) Load YAML
        var baseConfig = await _loader.LoadAsync(path, ct).ConfigureAwait(false);

        // 2) Validate static rules on the base config
        _validator.Validate(baseConfig);

        // 3) Apply runtime overrides (if any) via ConfigurationMerger
        var effective = ConfigurationMerger.ApplyOverrides(baseConfig, overrides);

        return effective;
    }
}
