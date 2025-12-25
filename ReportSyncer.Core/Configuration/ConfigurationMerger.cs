// ============================================================================
// File: ConfigurationMerger.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-05
// Description: Helper to apply runtime overrides to a base SyncConfiguration.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Centralized helper that applies <see cref="RuntimeOverrides"/> to a base
    /// <see cref="SyncConfiguration"/> to produce an immutable "effective" configuration
    /// instance used by the rest of the pipeline.
    /// </summary>
    internal static class ConfigurationMerger
    {
        /// <summary>
        /// Applies runtime overrides to <paramref name="baseConfig"/> and returns the effective configuration.
        /// </summary>
        /// <param name="baseConfig">The validated base configuration loaded from YAML (must not be null).</param>
        /// <param name="overrides">Optional runtime overrides. When null or empty, <paramref name="baseConfig"/> is returned as-is.</param>
        /// <returns>The effective <see cref="SyncConfiguration"/> to use for execution.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseConfig"/> is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when one or more override values are invalid.</exception>
        public static SyncConfiguration ApplyOverrides(
            SyncConfiguration baseConfig,
            RuntimeOverrides? overrides)
        {
            ArgumentNullException.ThrowIfNull(baseConfig);

            if (overrides == null || overrides.IsEmpty())
            {
                // No overrides; return the original validated instance.
                return baseConfig;
            }

            var errors = new List<ConfigurationError>();

            if (overrides.DefaultBatchSize.HasValue && overrides.DefaultBatchSize.Value <= 0)
            {
                errors.Add(ConfigurationErrors.InvalidBatchSize(overrides.DefaultBatchSize.Value));
            }

            if (overrides.DeleteChunkSize.HasValue && overrides.DeleteChunkSize.Value <= 0)
            {
                errors.Add(ConfigurationErrors.InvalidDeleteChunkSize(overrides.DeleteChunkSize.Value));
            }

            if (overrides.EtaSmoothing.HasValue)
            {
                var v = overrides.EtaSmoothing.Value;
                if (v < 0.0 || v > 1.0)
                    errors.Add(ConfigurationErrors.InvalidEtaSmoothing(overrides.EtaSmoothing));
            }

            if (errors.Count > 0)
            {
                throw new ConfigurationException(errors);
            }

            // Build a new RunConfig instance combining base values and overrides where provided.
            var baseRun = baseConfig.Run;

            var effectiveRun = new RunConfig(
                dryRun: overrides.DryRun ?? baseRun.DryRun,
                defaultBatchSize: overrides.DefaultBatchSize ?? baseRun.DefaultBatchSize,
                deleteChunkSize: overrides.DeleteChunkSize ?? baseRun.DeleteChunkSize,
                useTvpIfAvailable: overrides.UseTvpIfAvailable ?? baseRun.UseTvpIfAvailable,
                etaSmoothing: overrides.EtaSmoothing ?? baseRun.EtaSmoothing
            );

            // Create and return a new SyncConfiguration with the replaced Run section.
            return new SyncConfiguration(
                baseConfig.Version,
                effectiveRun,
                baseConfig.Safety,
                baseConfig.SchemaPolicy,
                baseConfig.Connections,
                baseConfig.SyncJobs
            );
        }
    }
}
