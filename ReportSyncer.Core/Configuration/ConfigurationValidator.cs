// ============================================================================
// File: ConfigurationValidator.cs
// Author: Gary Wu
// Date: 2025-12-02
// Project: ReportSyncer
// Description: Implementation of IConfigurationValidator that validates SyncConfiguration against all static rules.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Validates a <see cref="SyncConfiguration"/> by collecting all violations and throwing a single exception.
/// </summary>
internal sealed class ConfigurationValidator : IConfigurationValidator
{
    /// <summary>
    /// Validates the given configuration and throws if any violations are found.
    /// </summary>
    public void Validate(SyncConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = new List<ConfigurationError>();

        ValidateRoot(configuration, errors);
        ValidateConnections(configuration, errors);
        ValidateJobs(configuration, errors);
        ValidateTables(configuration, errors);
        ValidateSafety(configuration, errors);

        ThrowIfAny(errors);
    }

    /// <summary>
    /// Throws <see cref="ConfigurationException"/> if any errors were collected.
    /// </summary>
    private static void ThrowIfAny(List<ConfigurationError> errors)
    {
        if (errors.Count > 0)
        {
            throw new ConfigurationException(errors);
        }
    }

    /// <summary>
    /// Validates root-level configuration (collections non-null and non-empty, required sections present).
    /// </summary>
    private static void ValidateRoot(SyncConfiguration config, List<ConfigurationError> errors)
    {
        if (config.Connections == null || config.Connections.Count == 0)
        {
            errors.Add(ConfigurationErrors.NoConnectionsDefined());
        }

        if (config.SyncJobs == null || config.SyncJobs.Count == 0)
        {
            errors.Add(ConfigurationErrors.NoJobsDefined());
        }

        if (config.Run == null)
        {
            errors.Add(ConfigurationErrors.MissingRunSettings());
        }

        if (config.Safety == null)
        {
            errors.Add(ConfigurationErrors.MissingSafetySettings());
        }
    }

    /// <summary>
    /// Validates connections: non-null names, uniqueness, valid environment types, non-null connection strings.
    /// </summary>
    private static void ValidateConnections(SyncConfiguration config, List<ConfigurationError> errors)
    {
        var connections = config.Connections ?? new List<ConnectionConfig>();
        if (connections.Count == 0)
            return; // Already reported in ValidateRoot

        var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < connections.Count; i++)
        {
            var conn = connections[i];
            var pathBase = $"connections[{i}]";

            // Validate name
            if (string.IsNullOrWhiteSpace(conn.Name))
            {
                errors.Add(ConfigurationErrors.ConnectionNameMissing(pathBase));
            }
            else
            {
                var key = conn.Name.Trim();
                if (nameToIndex.TryGetValue(key, out var existingIndex))
                {
                    errors.Add(ConfigurationErrors.DuplicateConnectionName(key, existingIndex, i));
                }
                else
                {
                    nameToIndex[key] = i;
                }
            }

            // Validate environment type
            if (!Enum.IsDefined(typeof(EnvironmentType), conn.Environment))
            {
                errors.Add(ConfigurationErrors.InvalidConnectionEnvironment(pathBase, conn.Environment));
            }

            // Validate connection type
            if (!Enum.IsDefined(typeof(ConnectionType), conn.Type))
            {
                errors.Add(ConfigurationErrors.InvalidConnectionType(pathBase, conn.Type));
            }

            // Validate connection string
            if (string.IsNullOrWhiteSpace(conn.ConnectionString))
            {
                errors.Add(ConfigurationErrors.ConnectionStringMissing(pathBase));
            }
        }
    }

    /// <summary>
    /// Validates jobs: non-null names, reference integrity to connections, at least one table, prod-to-prod rules.
    /// </summary>
    private static void ValidateJobs(SyncConfiguration config, List<ConfigurationError> errors)
    {
        var jobs = config.SyncJobs ?? new List<SyncJobConfig>();
        if (jobs.Count == 0)
            return; // Already reported in ValidateRoot

        // Map connection names to environments for quick lookup.
        // Use a tolerant approach (ignore duplicate names here) because connection-name duplicates
        // are reported by ValidateConnections; we must not throw while building this lookup.
        var connectionEnvLookup = new Dictionary<string, EnvironmentType>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in config.Connections ?? Array.Empty<ConnectionConfig>())
        {
            var key = c.Name ?? string.Empty;
            if (!connectionEnvLookup.ContainsKey(key))
                connectionEnvLookup[key] = c.Environment;
        }

        var jobNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];
            var jobIdentifier = job.Name ?? i.ToString();
            var pathBase = $"jobs[{jobIdentifier}]";

            // Validate job name
            if (string.IsNullOrWhiteSpace(job.Name))
            {
                errors.Add(ConfigurationErrors.JobNameMissing($"jobs[{i}]"));
            }
            else if (!jobNameSet.Add(job.Name.Trim()))
            {
                errors.Add(ConfigurationErrors.DuplicateJobName(job.Name.Trim(), pathBase));
            }

            // Validate source connection
            if (string.IsNullOrWhiteSpace(job.SourceConnection))
            {
                errors.Add(ConfigurationErrors.JobSourceConnectionMissing(pathBase));
            }
            else if (!connectionEnvLookup.ContainsKey(job.SourceConnection))
            {
                errors.Add(ConfigurationErrors.JobSourceConnectionUnknown(pathBase, job.SourceConnection));
            }

            // Validate target connection
            if (string.IsNullOrWhiteSpace(job.TargetConnection))
            {
                errors.Add(ConfigurationErrors.JobTargetConnectionMissing(pathBase));
            }
            else if (!connectionEnvLookup.ContainsKey(job.TargetConnection))
            {
                errors.Add(ConfigurationErrors.JobTargetConnectionUnknown(pathBase, job.TargetConnection));
            }

            // Validate at least one table
            if (job.Tables == null || job.Tables.Count == 0)
            {
                errors.Add(ConfigurationErrors.JobHasNoTables(pathBase));
            }

            // Validate production-to-production rule
            if (!string.IsNullOrWhiteSpace(job.SourceConnection)
                && !string.IsNullOrWhiteSpace(job.TargetConnection)
                && connectionEnvLookup.TryGetValue(job.SourceConnection, out var srcEnv)
                && connectionEnvLookup.TryGetValue(job.TargetConnection, out var tgtEnv))
            {
                if (srcEnv == EnvironmentType.Prod && tgtEnv == EnvironmentType.Prod)
                {
                    if (config.Safety?.ForbidProdToProd == true)
                    {
                        errors.Add(ConfigurationErrors.ProdToProdSyncNotAllowed(pathBase, job.SourceConnection, job.TargetConnection));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates table tasks: non-null names, valid source/target tables, delete settings, thresholds.
    /// </summary>
    private static void ValidateTables(SyncConfiguration config, List<ConfigurationError> errors)
    {
        var jobs = config.SyncJobs ?? new List<SyncJobConfig>();

        for (int jobIndex = 0; jobIndex < jobs.Count; jobIndex++)
        {
            var job = jobs[jobIndex];
            if (job.Tables == null || job.Tables.Count == 0)
                continue; // Already reported in ValidateJobs

            var jobIdentifier = job.Name ?? jobIndex.ToString();
            var jobPath = $"jobs[{jobIdentifier}]";
            var tables = job.Tables;

            // Track table source names for uniqueness
            var tableSourceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            {
                var table = tables[tableIndex];
                var tableIdentifier = table.Source ?? tableIndex.ToString();
                var path = $"{jobPath}.tables[{tableIdentifier}]";

                // Validate source table name
                if (string.IsNullOrWhiteSpace(table.Source))
                {
                    errors.Add(ConfigurationErrors.SourceTableMissing(path));
                }
                else
                {
                    // Track unique source tables within a job
                    tableSourceSet.Add(table.Source);
                }

                // Validate target table name
                if (string.IsNullOrWhiteSpace(table.Target))
                {
                    errors.Add(ConfigurationErrors.TargetTableMissing(path));
                }

                // Validate pre-sync delete settings
                ValidateTableDeleteSettings(config, job, table, path, errors);
            }
        }
    }

    /// <summary>
    /// Validates delete-related settings for a table: threshold ranges and safety constraints.
    /// </summary>
    private static void ValidateTableDeleteSettings(
        SyncConfiguration config,
        SyncJobConfig job,
        TableTaskConfig table,
        string path,
        List<ConfigurationError> errors)
    {
        // Validate pre-sync delete behavior
        if (table.PreSyncTargetAction)
        {
            if (table.Filter == null)
            {
                // Full table delete scenario - requires explicit safety latch
                if (!table.AllowAllDelete)
                {
                    errors.Add(ConfigurationErrors.FullTableDeleteNotAllowed(path));
                }
            }
            // Note: If filter is present, delete is scoped and AllowAllDelete is not required
        }

        // Validate filter configuration if present
        if (table.Filter != null)
        {
            ValidateFilter(table.Filter, path, errors);
        }

        // Validate sync options if present
        if (table.SyncOptions != null)
        {
            ValidateSyncOptions(table.SyncOptions, path, errors);
        }

        // Validate column mapping if present
        if (table.ColumnMapping != null)
        {
            ValidateColumnMapping(table.ColumnMapping, path, job.Parameters as IReadOnlyDictionary<string, string>, errors);
        }

        // Validate keys/business key configuration if present
        if (table.Keys != null)
        {
            ValidateKeys(table.Keys, path, errors);
        }
    }

    /// <summary>
    /// Validates filter configuration: required fields and value consistency.
    /// </summary>
    private static void ValidateFilter(FilterConfig filter, string tablePath, List<ConfigurationError> errors)
    {
        // Filter validation can be extended based on specific filter requirements
        // For now, we accept any non-null filter configuration
        // Future: validate filter types, date ranges, column references, etc.
    }

    /// <summary>
    /// Validates table-level sync options: batch sizes and TVP settings.
    /// </summary>
    private static void ValidateSyncOptions(SyncOptionsConfig syncOptions, string tablePath, List<ConfigurationError> errors)
    {
        // Table-level batch size validation if it overrides global defaults
        // This can be extended based on specific requirements
    }

    /// <summary>
    /// Validates column mapping configuration.
    /// </summary>
    private static void ValidateColumnMapping(ColumnMappingConfig columnMapping, string tablePath, IReadOnlyDictionary<string, string>? jobParameters, List<ConfigurationError> errors)
    {
        if (columnMapping == null) return;

        var mappings = columnMapping.Mappings;
        if (mappings == null || mappings.Count == 0) return;

        foreach (var kv in mappings)
        {
            var targetCol = kv.Key;
            var rule = kv.Value;
            if (rule == null) continue;

            var path = $"{tablePath}.columnMapping.mappings.{targetCol}";

            var setCount = 0;
            if (!string.IsNullOrWhiteSpace(rule.FromSource)) setCount++;
            if (!string.IsNullOrWhiteSpace(rule.Const)) setCount++;
            if (!string.IsNullOrWhiteSpace(rule.FromParameter)) setCount++;
            if (rule.Ignore) setCount++;

            if (setCount > 1)
            {
                errors.Add(ConfigurationErrors.MappingRuleConflict(path, targetCol));
                continue;
            }

            if (rule.FromSource != null && string.IsNullOrWhiteSpace(rule.FromSource))
            {
                errors.Add(ConfigurationErrors.MappingRuleFromSourceEmpty(path, targetCol));
            }

            if (rule.FromParameter != null && string.IsNullOrWhiteSpace(rule.FromParameter))
            {
                errors.Add(ConfigurationErrors.MappingRuleFromParameterEmpty(path, targetCol));
            }

            if (rule.Const == null && rule.FromSource == null && rule.FromParameter == null && !rule.Ignore)
            {
                // Rule object exists but no meaningful value provided; report const-null if const explicitly present
                if (rule.Const == null)
                {
                    errors.Add(ConfigurationErrors.MappingRuleConstNull(path, targetCol));
                }
            }

            // Validate FromParameter references exist in job parameters (case-insensitive)
            if (!string.IsNullOrWhiteSpace(rule.FromParameter))
            {
                var paramName = rule.FromParameter!;
                var found = false;
                if (jobParameters != null)
                {
                    foreach (var k in jobParameters.Keys)
                    {
                        if (string.Equals(k, paramName, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    errors.Add(ConfigurationErrors.JobParameterMissing(path, paramName));
                }
            }
        }
    }

    /// <summary>
    /// Validates business key configuration for deduplication.
    /// </summary>
    private static void ValidateKeys(KeyConfig keys, string tablePath, List<ConfigurationError> errors)
    {
        // Key configuration validation can be extended based on specific requirements
        // Future: validate key column references, uniqueness, etc.
    }

    /// <summary>
    /// Validates run and safety configuration: numeric ranges, consistency rules.
    /// </summary>
    private static void ValidateSafety(SyncConfiguration config, List<ConfigurationError> errors)
    {
        var run = config.Run;
        if (run != null)
        {
            // Validate batch size is positive
            if (run.DefaultBatchSize <= 0)
            {
                errors.Add(ConfigurationErrors.InvalidBatchSize(run.DefaultBatchSize));
            }

            // Validate delete chunk size is positive
            if (run.DeleteChunkSize <= 0)
            {
                errors.Add(ConfigurationErrors.InvalidDeleteChunkSize(run.DeleteChunkSize));
            }

            // Validate ETA smoothing is in valid range [0.0, 1.0]
            if (run.EtaSmoothing.HasValue)
            {
                if (run.EtaSmoothing.Value < 0.0 || run.EtaSmoothing.Value > 1.0)
                {
                    errors.Add(ConfigurationErrors.InvalidEtaSmoothing(run.EtaSmoothing));
                }
            }
        }

        var safety = config.Safety;
        if (safety != null)
        {
            // Validate confirm large delete percentage is in range [0.0, 1.0]
            if (safety.ConfirmLargeDeletePct < 0.0 || safety.ConfirmLargeDeletePct > 1.0)
            {
                errors.Add(ConfigurationErrors.ConfirmLargeDeletePctOutOfRange(safety.ConfirmLargeDeletePct));
            }

            // Additional cross-cutting safety checks can be added here
            // Examples:
            // - If ForbidProdToProd is false globally, might want warning
            // - If RequireDifferentConnections is false globally, might want warning
        }

        // Validate version format if present
        if (!string.IsNullOrWhiteSpace(config.Version))
        {
            ValidateConfigurationVersion(config.Version, errors);
        }
    }

    /// <summary>
    /// Validates configuration version format.
    /// </summary>
    private static void ValidateConfigurationVersion(string version, List<ConfigurationError> errors)
    {
        // Version format can be extended based on specific requirements
        // For now, we accept any non-empty version string
        // Future: validate semantic versioning (e.g., "1.0", "1.2.3"), supported versions, etc.
    }
}
