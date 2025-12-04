// ============================================================================
// File: ConfigurationErrors.cs
// Author: Gary Wu
// Date: 2025-12-02
// Project: ReportSyncer
// Description: Factory methods for configuration validation errors with consistent codes and messages.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Factory methods for creating consistent configuration validation errors.
/// </summary>
/// <remarks>
/// This static class centralizes error creation to ensure that:
/// <list type="bullet">
/// <item><description>Error codes are stable and recognizable for host-layer mapping</description></item>
/// <item><description>Error messages are clear and user-oriented</description></item>
/// <item><description>Path information is consistently provided</description></item>
/// </list>
/// </remarks>
internal static class ConfigurationErrors
{
    // ============================================================================
    // Root-Level Errors
    // ============================================================================

    /// <summary>
    /// Error: Configuration root object is null.
    /// </summary>
    public static ConfigurationError NullRootConfiguration() =>
        new("CFG_ROOT_NULL", "Configuration root object is null.", path: null);

    /// <summary>
    /// Error: Connections collection is null.
    /// </summary>
    public static ConfigurationError NullConnectionsCollection() =>
        new("CFG_CONNECTIONS_NULL", "Connections collection is null.", "connections");

    /// <summary>
    /// Error: Jobs collection is null.
    /// </summary>
    public static ConfigurationError NullJobsCollection() =>
        new("CFG_JOBS_NULL", "Sync jobs collection is null.", "jobs");

    /// <summary>
    /// Error: Run settings are missing.
    /// </summary>
    public static ConfigurationError MissingRunSettings() =>
        new("CFG_RUN_MISSING", "Run configuration section is required and must not be null.", "run");

    /// <summary>
    /// Error: Safety settings are missing.
    /// </summary>
    public static ConfigurationError MissingSafetySettings() =>
        new("CFG_SAFETY_MISSING", "Safety configuration section is required and must not be null.", "safety");

    // ============================================================================
    // Connection Validation Errors
    // ============================================================================

    /// <summary>
    /// Error: No connections defined.
    /// </summary>
    public static ConfigurationError NoConnectionsDefined() =>
        new("CFG_CONNECTIONS_EMPTY", "At least one connection must be defined.", "connections");

    /// <summary>
    /// Error: Connection name is missing or empty.
    /// </summary>
    public static ConfigurationError ConnectionNameMissing(string path) =>
        new("CFG_CONNECTION_NAME_MISSING", "Connection name is required and cannot be empty or whitespace.", path);

    /// <summary>
    /// Error: Duplicate connection name.
    /// </summary>
    public static ConfigurationError DuplicateConnectionName(string name, int firstIndex, int secondIndex) =>
        new(
            "CFG_CONNECTION_NAME_DUPLICATE",
            $"Connection name '{name}' appears multiple times (at indices {firstIndex} and {secondIndex}). Connection names must be unique (case-insensitive).",
            $"connections[{secondIndex}]");

    /// <summary>
    /// Error: Invalid environment type for connection.
    /// </summary>
    public static ConfigurationError InvalidConnectionEnvironment(string path, EnvironmentType environment) =>
        new(
            "CFG_CONNECTION_ENV_INVALID",
            $"Connection environment type '{environment}' is invalid or unsupported.",
            path);

    /// <summary>
    /// Error: Invalid connection type for connection.
    /// </summary>
    public static ConfigurationError InvalidConnectionType(string path, ConnectionType type) =>
        new(
            "CFG_CONNECTION_TYPE_INVALID",
            $"Connection type '{type}' is invalid or unsupported.",
            path);

    /// <summary>
    /// Error: Connection string is missing or empty.
    /// </summary>
    public static ConfigurationError ConnectionStringMissing(string path) =>
        new(
            "CFG_CONNECTION_STRING_MISSING",
            "Connection string is required and cannot be empty or whitespace.",
            path);

    // ============================================================================
    // Job Validation Errors
    // ============================================================================

    /// <summary>
    /// Error: No jobs defined.
    /// </summary>
    public static ConfigurationError NoJobsDefined() =>
        new("CFG_JOBS_EMPTY", "At least one sync job must be defined.", "jobs");

    /// <summary>
    /// Error: Job name is missing or empty.
    /// </summary>
    public static ConfigurationError JobNameMissing(string path) =>
        new("CFG_JOB_NAME_MISSING", "Job name is required and cannot be empty or whitespace.", path);

    /// <summary>
    /// Error: Duplicate job name.
    /// </summary>
    public static ConfigurationError DuplicateJobName(string name, string path) =>
        new(
            "CFG_JOB_NAME_DUPLICATE",
            $"Job name '{name}' is duplicated. Job names must be unique (case-insensitive).",
            path);

    /// <summary>
    /// Error: Source connection name is missing.
    /// </summary>
    public static ConfigurationError JobSourceConnectionMissing(string path) =>
        new(
            "CFG_JOB_SOURCE_CONNECTION_MISSING",
            "Source connection name is required and cannot be empty or whitespace.",
            path);

    /// <summary>
    /// Error: Source connection reference is unknown.
    /// </summary>
    public static ConfigurationError JobSourceConnectionUnknown(string path, string connectionName) =>
        new(
            "CFG_JOB_SOURCE_CONNECTION_UNKNOWN",
            $"Source connection '{connectionName}' does not exist in the connections list.",
            path);

    /// <summary>
    /// Error: Target connection name is missing.
    /// </summary>
    public static ConfigurationError JobTargetConnectionMissing(string path) =>
        new(
            "CFG_JOB_TARGET_CONNECTION_MISSING",
            "Target connection name is required and cannot be empty or whitespace.",
            path);

    /// <summary>
    /// Error: Target connection reference is unknown.
    /// </summary>
    public static ConfigurationError JobTargetConnectionUnknown(string path, string connectionName) =>
        new(
            "CFG_JOB_TARGET_CONNECTION_UNKNOWN",
            $"Target connection '{connectionName}' does not exist in the connections list.",
            path);

    /// <summary>
    /// Error: Job has no table tasks.
    /// </summary>
    public static ConfigurationError JobHasNoTables(string path) =>
        new(
            "CFG_JOB_NO_TABLES",
            "Job must contain at least one table task.",
            path);

    /// <summary>
    /// Error: Production-to-Production sync not allowed.
    /// </summary>
    public static ConfigurationError ProdToProdSyncNotAllowed(string path, string sourceConnection, string targetConnection) =>
        new(
            "CFG_PROD_TO_PROD_FORBIDDEN",
            $"Production-to-Production sync is forbidden: source '{sourceConnection}' and target '{targetConnection}' are both Production environments. " +
            "Set 'safety.forbidProdToProd' to false in configuration or ensure one connection is not Production.",
            path);

    // ============================================================================
    // Table Task Validation Errors
    // ============================================================================

    /// <summary>
    /// Error: Table name is missing or empty.
    /// </summary>
    public static ConfigurationError TableNameMissing(string path) =>
        new(
            "CFG_TABLE_NAME_MISSING",
            "Table task name is required and cannot be empty or whitespace.",
            path);

    /// <summary>
    /// Error: Source table name is missing or empty.
    /// </summary>
    public static ConfigurationError SourceTableMissing(string path) =>
        new(
            "CFG_TABLE_SOURCE_MISSING",
            "Source table name is required and cannot be empty or whitespace.",
            path);

    /// <summary>
    /// Error: Target table name is missing or empty.
    /// </summary>
    public static ConfigurationError TargetTableMissing(string path) =>
        new(
            "CFG_TABLE_TARGET_MISSING",
            "Target table name is required and cannot be empty or whitespace.",
            path);

    /// <summary>
    /// Error: Delete threshold is out of valid range.
    /// </summary>
    public static ConfigurationError DeleteThresholdOutOfRange(string path, double threshold) =>
        new(
            "CFG_DELETE_THRESHOLD_INVALID",
            $"Delete threshold '{threshold}' is out of valid range. Threshold must be between 0 (exclusive) and 1 (inclusive).",
            path);

    /// <summary>
    /// Error: Full table delete not allowed due to safety constraints.
    /// </summary>
    public static ConfigurationError FullTableDeleteNotAllowed(string path) =>
        new(
            "CFG_FULL_TABLE_DELETE_FORBIDDEN",
            "Full table delete is not allowed: PreSyncTargetAction is true, filter is null, but AllowAllDelete is false. " +
            "To perform an unscoped delete (all rows), you must explicitly set 'allowAllDelete' to true in the table configuration.",
            path);

    // ============================================================================
    // Run Configuration Errors
    // ============================================================================

    /// <summary>
    /// Error: Batch size is invalid (must be positive).
    /// </summary>
    public static ConfigurationError InvalidBatchSize(int batchSize) =>
        new(
            "CFG_BATCH_SIZE_INVALID",
            $"Batch size '{batchSize}' is invalid. Batch size must be a positive integer.",
            "run.defaultBatchSize");

    /// <summary>
    /// Error: Delete chunk size is invalid (must be positive).
    /// </summary>
    public static ConfigurationError InvalidDeleteChunkSize(int chunkSize) =>
        new(
            "CFG_DELETE_CHUNK_SIZE_INVALID",
            $"Delete chunk size '{chunkSize}' is invalid. Delete chunk size must be a positive integer.",
            "run.deleteChunkSize");

    /// <summary>
    /// Error: ETA smoothing value is out of valid range.
    /// </summary>
    public static ConfigurationError InvalidEtaSmoothing(double? etaSmoothing) =>
        new(
            "CFG_ETA_SMOOTHING_INVALID",
            $"ETA smoothing value '{etaSmoothing}' is invalid. ETA smoothing must be between 0.0 and 1.0 (inclusive).",
            "run.etaSmoothing");

    // ============================================================================
    // Safety Configuration Errors
    // ============================================================================

    /// <summary>
    /// Error: Default delete threshold is out of valid range.
    /// </summary>
    public static ConfigurationError DefaultDeleteThresholdOutOfRange(double threshold) =>
        new(
            "CFG_DEFAULT_DELETE_THRESHOLD_INVALID",
            $"Default delete threshold '{threshold}' is out of valid range. Threshold must be between 0 (exclusive) and 1 (inclusive).",
            "safety.confirmLargeDeletePct");

    /// <summary>
    /// Error: Confirm large delete percentage is out of valid range.
    /// </summary>
    public static ConfigurationError ConfirmLargeDeletePctOutOfRange(double percentage) =>
        new(
            "CFG_CONFIRM_DELETE_PCT_INVALID",
            $"Confirm large delete percentage '{percentage}' is invalid. Percentage must be between 0.0 and 1.0 (inclusive).",
            "safety.confirmLargeDeletePct");
}
