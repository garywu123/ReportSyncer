# Task 2.3 – Configuration Validation Detailed Implementation Plan

> Scope: `ReportSyncer.Core.Configuration` (and closely related domain types). This plan assumes Tasks 2.1 (domain model) and 2.2 (YAML loading) are conceptually in place or at least roughly shaped as per the architecture instructions.

---

## 0. Context & Assumptions

1. **Root configuration type**

   * There is a root domain model type (name from 2.1), e.g. `SyncConfiguration`, living under `ReportSyncer.Core.Configuration`.
   * High-level structure (logical, not exact):

     ```csharp
     public sealed class SyncConfiguration
     {
         public RunSettings Run { get; }
         public SafetySettings Safety { get; }
         public IReadOnlyList<ConnectionConfiguration> Connections { get; }
         public IReadOnlyList<SyncJobConfiguration> Jobs { get; }
     }
     ```

2. **Connections & jobs**

   * `ConnectionConfiguration` exposes at least:

     ```csharp
     public sealed class ConnectionConfiguration
     {
         public string Name { get; }
         public EnvironmentType Environment { get; } // Dev, Test, Prod, etc.
         public DatabaseKind DatabaseKind { get; }   // Application, Reporting, etc. as per PRD
         public string ConnectionStringName { get; } // or equivalent
     }
     ```
   * `SyncJobConfiguration` exposes at least:

     ```csharp
     public sealed class SyncJobConfiguration
     {
         public string Name { get; }
         public string SourceConnectionName { get; }
         public string TargetConnectionName { get; }
         public IReadOnlyList<TableTaskConfiguration> Tables { get; }
         public JobSafetySettings Safety { get; } // or safety flags merged from root/job level
     }
     ```

3. **Table task & safety**

   * `TableTaskConfiguration` exposes at least:

     ```csharp
     public sealed class TableTaskConfiguration
     {
         public string Name { get; }                 // logical name / identifier
         public string SourceTable { get; }
         public string TargetTable { get; }
         public PreSyncAction PreSyncAction { get; } // enum/flags for delete modes etc.
         public TableSafetySettings Safety { get; }
         public DeleteThresholdSettings DeleteThreshold { get; } // nullable
         public IReadOnlyList<TableFilterConfiguration> Filters { get; }
     }
     ```

4. **Error model**

   * Section 2.5 will introduce a configuration-specific exception type, e.g.:

     ```csharp
     public sealed class ConfigurationException : Exception
     {
         public IReadOnlyList<ConfigurationError> Errors { get; }
         // ...
     }

     public sealed class ConfigurationError
     {
         public string Code { get; }
         public string Message { get; }
         public string? Path { get; }    // e.g. "jobs[Job1].tables[0].deleteThreshold"
     }
     ```
   * For Task 2.3 we **implement the error aggregation pattern** even if 2.5 is not fully wired yet. Types can live in `ReportSyncer.Core.Configuration` or a sub-namespace like `.Errors`.

5. **No DB / runtime dependency**

   * Validator must **only** inspect configuration objects. No network/DB calls, no schema inspection, no logging.

---

## 1. High-level Design

### 1.1 Interfaces & main implementation

Create a dedicated validator abstraction in `ReportSyncer.Core.Configuration`:

```csharp
namespace ReportSyncer.Core.Configuration
{
    public interface IConfigurationValidator
    {
        /// <summary>
        /// Validates the given configuration. Throws <see cref="ConfigurationException"/>
        /// when configuration is invalid.
        /// </summary>
        void Validate(SyncConfiguration configuration);
    }
}
```

Concrete implementation:

```csharp
namespace ReportSyncer.Core.Configuration
{
    internal sealed class ConfigurationValidator : IConfigurationValidator
    {
        public void Validate(SyncConfiguration configuration)
        {
            var errors = new List<ConfigurationError>();

            if (configuration is null)
            {
                errors.Add(ConfigurationErrors.NullRootConfiguration());
                ThrowIfAny(errors);
                return;
            }

            ValidateRoot(configuration, errors);
            ValidateConnections(configuration, errors);
            ValidateJobs(configuration, errors);
            ValidateTables(configuration, errors);
            ValidateSafety(configuration, errors);

            ThrowIfAny(errors);
        }

        private static void ThrowIfAny(List<ConfigurationError> errors)
        {
            if (errors.Count > 0)
            {
                throw new ConfigurationException(errors);
            }
        }

        // private helper methods below ...
    }
}
```

Optional: introduce a static helper class `ConfigurationErrors` holding factory methods for common error instances with consistent `Code` and `Message` patterns.

### 1.2 Validation philosophy

* **Fail fast, but collect multiple errors**: collect all issues in a single pass, then throw one `ConfigurationException` containing all `ConfigurationError`s.
* **Always provide a path**: every error should include a `Path` string describing exactly where the problem sits in the config tree.
* **No side effects**: validation must not mutate the configuration objects.

---

## 2. Detailed Validation Rules & Implementation Steps

Group implementation into logical stages; each stage corresponds to concrete private methods in `ConfigurationValidator`.

### 2.1 Root-level validation (`ValidateRoot`)

**Rules**

1. Root object is non-null (already handled at entry).
2. `Connections` collection not null.
3. `Jobs` collection not null.
4. Optional: `Run` and `Safety` sections must not be null if PRD says they are required.

**Implementation**

```csharp
private static void ValidateRoot(SyncConfiguration config, List<ConfigurationError> errors)
{
    if (config.Connections is null)
    {
        errors.Add(ConfigurationErrors.NullConnectionsCollection());
    }

    if (config.Jobs is null)
    {
        errors.Add(ConfigurationErrors.NullJobsCollection());
    }

    // If Run/Safety required:
    if (config.Run is null)
    {
        errors.Add(ConfigurationErrors.MissingRunSettings());
    }

    if (config.Safety is null)
    {
        errors.Add(ConfigurationErrors.MissingSafetySettings());
    }
}
```

**Path examples**

* `"connections"`
* `"jobs"`
* `"run"`, `"safety"`

### 2.2 Connection validation (`ValidateConnections`)

**Rules**

1. There must be **at least one connection**.
2. Each connection must have a **non-empty, non-whitespace Name**.
3. Connection names must be **unique** (case-insensitive).
4. `Environment` must be a valid enum value; no `Unknown` / `None` if such exists.
5. `DatabaseKind` must be a valid enum; only supported kinds as per PRD.
6. Any required fields (connection string references, etc.) must be non-empty.

**Implementation sketch**

```csharp
private static void ValidateConnections(SyncConfiguration config, List<ConfigurationError> errors)
{
    var connections = config.Connections ?? Array.Empty<ConnectionConfiguration>();

    if (connections.Count == 0)
    {
        errors.Add(ConfigurationErrors.NoConnectionsDefined());
        return;
    }

    var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < connections.Count; i++)
    {
        var conn = connections[i];
        var pathBase = $"connections[{i}]";

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

        if (!Enum.IsDefined(typeof(EnvironmentType), conn.Environment) || conn.Environment == EnvironmentType.Unknown)
        {
            errors.Add(ConfigurationErrors.InvalidConnectionEnvironment(pathBase, conn.Environment));
        }

        if (!Enum.IsDefined(typeof(DatabaseKind), conn.DatabaseKind))
        {
            errors.Add(ConfigurationErrors.InvalidDatabaseKind(pathBase, conn.DatabaseKind));
        }

        if (string.IsNullOrWhiteSpace(conn.ConnectionStringName))
        {
            errors.Add(ConfigurationErrors.ConnectionStringNameMissing(pathBase));
        }
    }
}
```

**Key error codes** (examples):

* `CFG_CONNECTIONS_EMPTY`
* `CFG_CONNECTION_NAME_MISSING`
* `CFG_CONNECTION_NAME_DUPLICATE`
* `CFG_CONNECTION_ENV_INVALID`
* `CFG_CONNECTION_DBKIND_INVALID`
* `CFG_CONNECTION_STRING_NAME_MISSING`

### 2.3 Job-level validation (`ValidateJobs`)

**Rules**

1. There must be **at least one job**.
2. Each job must have:

   * Non-empty `Name`.
   * Non-empty `SourceConnectionName` and `TargetConnectionName`.
3. Both connection names must exist in `Connections` list (case-insensitive match).
4. Each job must have **at least one table task**.
5. Optional: job names must be unique (case-insensitive) if PRD requires.
6. Environmental rules (high-level):

   * Prod→Prod sync **forbidden** unless safety flags explicitly allow (based on PRD safety section).
     *For validation we only check what we can from env flags plus config-level safety.*

**Implementation sketch**

```csharp
private static void ValidateJobs(SyncConfiguration config, List<ConfigurationError> errors)
{
    var jobs = config.Jobs ?? Array.Empty<SyncJobConfiguration>();

    if (jobs.Count == 0)
    {
        errors.Add(ConfigurationErrors.NoJobsDefined());
        return;
    }

    // Map connection name -> environment for quick lookup
    var connectionEnvLookup = (config.Connections ?? Array.Empty<ConnectionConfiguration>())
        .ToDictionary(c => c.Name, c => c.Environment, StringComparer.OrdinalIgnoreCase);

    var jobNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < jobs.Count; i++)
    {
        var job = jobs[i];
        var pathBase = $"jobs[{job.Name ?? i.ToString()}]";

        if (string.IsNullOrWhiteSpace(job.Name))
        {
            errors.Add(ConfigurationErrors.JobNameMissing($"jobs[{i}]"));
        }
        else if (!jobNameSet.Add(job.Name.Trim()))
        {
            errors.Add(ConfigurationErrors.DuplicateJobName(job.Name.Trim(), pathBase));
        }

        if (string.IsNullOrWhiteSpace(job.SourceConnectionName))
        {
            errors.Add(ConfigurationErrors.JobSourceConnectionMissing(pathBase));
        }
        else if (!connectionEnvLookup.ContainsKey(job.SourceConnectionName))
        {
            errors.Add(ConfigurationErrors.JobSourceConnectionUnknown(pathBase, job.SourceConnectionName));
        }

        if (string.IsNullOrWhiteSpace(job.TargetConnectionName))
        {
            errors.Add(ConfigurationErrors.JobTargetConnectionMissing(pathBase));
        }
        else if (!connectionEnvLookup.ContainsKey(job.TargetConnectionName))
        {
            errors.Add(ConfigurationErrors.JobTargetConnectionUnknown(pathBase, job.TargetConnectionName));
        }

        // At least one table
        if (job.Tables == null || job.Tables.Count == 0)
        {
            errors.Add(ConfigurationErrors.JobHasNoTables(pathBase));
        }

        // Environment safety rule: detect prod->prod
        if (!string.IsNullOrWhiteSpace(job.SourceConnectionName)
            && !string.IsNullOrWhiteSpace(job.TargetConnectionName)
            && connectionEnvLookup.TryGetValue(job.SourceConnectionName, out var srcEnv)
            && connectionEnvLookup.TryGetValue(job.TargetConnectionName, out var tgtEnv))
        {
            if (srcEnv == EnvironmentType.Prod && tgtEnv == EnvironmentType.Prod)
            {
                if (!JobAllowsProdToProd(job, config))
                {
                    errors.Add(ConfigurationErrors.ProdToProdSyncNotAllowed(pathBase, job.SourceConnectionName, job.TargetConnectionName));
                }
            }
        }
    }
}
```

Helper `JobAllowsProdToProd` will inspect the job’s safety settings vs root/global safety as per PRD rules (e.g. explicit `allowProdToProd: true` with some confirmation flag).

### 2.4 Table-level validation (`ValidateTables`)

This operates across all jobs.

**Rules**

1. Each table must have non-empty `Name`, `SourceTable`, `TargetTable`.
2. Pre-sync delete/actions must be consistent with safety flags.

   * Example rule pattern:

     * If `PreSyncAction.DeleteMode == DeleteMode.FullTable` then there must be **explicit safety latch** set at table or job level (e.g. `Safety.AllowFullTableDelete == true`).
3. Delete thresholds must be in the correct range:

   * If represented as fraction: 0 < threshold ≤ 1.
   * If represented as percentage: 0 < threshold ≤ 100.
   * Exact rule to be aligned with PRD.
4. Optional: if table is configured to perform destructive operations without any filter, apply stricter rules (depending on PRD wording).

**Implementation sketch**

```csharp
private static void ValidateTables(SyncConfiguration config, List<ConfigurationError> errors)
{
    var jobs = config.Jobs ?? Array.Empty<SyncJobConfiguration>();

    for (int jobIndex = 0; jobIndex < jobs.Count; jobIndex++)
    {
        var job = jobs[jobIndex];
        var jobPath = $"jobs[{job.Name ?? jobIndex.ToString()}]";
        var tables = job.Tables ?? Array.Empty<TableTaskConfiguration>();

        for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            var table = tables[tableIndex];
            var path = $"{jobPath}.tables[{table.Name ?? tableIndex.ToString()}]";

            if (string.IsNullOrWhiteSpace(table.Name))
            {
                errors.Add(ConfigurationErrors.TableNameMissing(path));
            }
            if (string.IsNullOrWhiteSpace(table.SourceTable))
            {
                errors.Add(ConfigurationErrors.SourceTableMissing(path));
            }
            if (string.IsNullOrWhiteSpace(table.TargetTable))
            {
                errors.Add(ConfigurationErrors.TargetTableMissing(path));
            }

            ValidateTableDeleteSettings(config, job, table, path, errors);
        }
    }
}
```

`ValidateTableDeleteSettings` handles safety and thresholds:

```csharp
private static void ValidateTableDeleteSettings(
    SyncConfiguration config,
    SyncJobConfiguration job,
    TableTaskConfiguration table,
    string path,
    List<ConfigurationError> errors)
{
    var deleteMode = table.PreSyncAction.DeleteMode;

    // Example large-delete safety
    if (table.DeleteThreshold is { } threshold)
    {
        if (threshold.Value <= 0 || threshold.Value > 1)
        {
            errors.Add(ConfigurationErrors.DeleteThresholdOutOfRange(path, threshold.Value));
        }
    }

    // Example full-delete safety
    if (deleteMode == DeleteMode.FullTable)
    {
        if (!TableAllowsFullDelete(config, job, table))
        {
            errors.Add(ConfigurationErrors.FullTableDeleteNotAllowed(path));
        }
    }
}
```

Actual rules inside `TableAllowsFullDelete` must mirror PRD wording:

* E.g. require `Safety.AllowFullTableDelete == true` **and** `Safety.RequireExplicitTableList == true` or similar.

### 2.5 Run-level & safety-level validation (`ValidateSafety`)

This method checks cross-cutting safety rules that don’t belong solely to jobs or tables.

**Rules** (examples, to be synced with PRD):

1. Global run options must be sane:

   * Batch size > 0.
   * Max parallel jobs > 0 (if configured).
2. Safety settings are internally consistent:

   * Large-delete threshold percentage/fraction constraints.
   * If global flag `ForbidProdToProd` is enabled, then no job should override it unless PRD allows explicit opt-out.
   * If `RequireDryRunBeforeProd` like flags exist, ensure they are usable (some configs may be impossible to satisfy).

**Implementation sketch**

```csharp
private static void ValidateSafety(SyncConfiguration config, List<ConfigurationError> errors)
{
    var run = config.Run;
    if (run != null)
    {
        if (run.BatchSize <= 0)
        {
            errors.Add(ConfigurationErrors.InvalidBatchSize(run.BatchSize));
        }

        if (run.MaxParallelJobs.HasValue && run.MaxParallelJobs.Value <= 0)
        {
            errors.Add(ConfigurationErrors.InvalidMaxParallelJobs(run.MaxParallelJobs.Value));
        }
    }

    var safety = config.Safety;
    if (safety != null)
    {
        if (safety.DefaultDeleteThreshold.HasValue
            && (safety.DefaultDeleteThreshold <= 0 || safety.DefaultDeleteThreshold > 1))
        {
            errors.Add(ConfigurationErrors.DefaultDeleteThresholdOutOfRange(safety.DefaultDeleteThreshold.Value));
        }

        // Additional cross-cutting safety checks as per PRD can be plugged here.
    }
}
```

---

## 3. Error Representation & Factory Helper

Introduce a small helper class to centralize error creation and ensure consistent codes/messages.

```csharp
internal static class ConfigurationErrors
{
    public static ConfigurationError NullRootConfiguration() =>
        new("CFG_ROOT_NULL", "Configuration root object is null.", path: null);

    public static ConfigurationError NullConnectionsCollection() =>
        new("CFG_CONNECTIONS_NULL", "Connections collection is null.", "connections");

    public static ConfigurationError NoConnectionsDefined() =>
        new("CFG_CONNECTIONS_EMPTY", "At least one connection must be defined.", "connections");

    public static ConfigurationError ConnectionNameMissing(string path) =>
        new("CFG_CONNECTION_NAME_MISSING", "Connection name is required.", path);

    public static ConfigurationError DuplicateConnectionName(string name, int firstIndex, int secondIndex) =>
        new(
            "CFG_CONNECTION_NAME_DUPLICATE",
            $"Connection name '{name}' is duplicated (indices {firstIndex} and {secondIndex}).",
            path: $"connections[{secondIndex}]" );

    // ... similar factories for all rules above
}
```

The goal is not to enumerate every factory here, but to be consistent: `Code` is stable for tests and host mapping, `Message` is human-readable and `Path` points to the problematic node.

---

## 4. Unit Tests Plan (ReportSyncer.Core.Tests)

Create a test folder/namespace: `ReportSyncer.Core.Tests.Configuration` with a main class `ConfigurationValidatorTests`.

Each test should build a minimal in-memory `SyncConfiguration` instance and call `new ConfigurationValidator().Validate(config)`.

### 4.1 Positive tests

1. `Validate_WithValidMinimalConfiguration_DoesNotThrow`

   * Build the smallest config that satisfies all rules.
   * Assert no exception.

2. `Validate_WithValidFullConfiguration_DoesNotThrow`

   * Build a representative config with multiple connections/jobs/tables and safety settings.

### 4.2 Negative tests (samples)

1. `Validate_WithNoConnections_ThrowsConfigurationExceptionWithConnectionsError`

   * `Connections = []`.
   * Expect `ConfigurationException` with one error code `CFG_CONNECTIONS_EMPTY`.

2. `Validate_WithDuplicateConnectionNames_ThrowsConfigurationException`

   * Two connections named `"Reporting"`.
   * Check error with code `CFG_CONNECTION_NAME_DUPLICATE` and correct path.

3. `Validate_WithUnknownJobSourceConnection_ThrowsConfigurationException`

   * Job references a non-existing connection.

4. `Validate_WithNoJobs_ThrowsConfigurationException`

   * `Jobs = []`.

5. `Validate_WithJobWithoutTables_ThrowsConfigurationException`

   * `Jobs[0].Tables = []`.

6. `Validate_WithProdToProdSyncWithoutOverride_ThrowsConfigurationException`

   * Source and target connections both `EnvironmentType.Prod`.
   * Safety flags do not explicitly allow.

7. `Validate_WithTableMissingSourceOrTarget_ThrowsConfigurationException`

   * Missing `SourceTable` or `TargetTable`.

8. `Validate_WithFullTableDeleteWithoutSafetyFlag_ThrowsConfigurationException`

   * Table `PreSyncAction.DeleteMode = FullTable` but safety flags are false.

9. `Validate_WithInvalidDeleteThreshold_ThrowsConfigurationException`

   * `DeleteThreshold = 0` or `> 1`.

10. `Validate_WithInvalidRunBatchSize_ThrowsConfigurationException`

    * `Run.BatchSize <= 0`.

### 4.3 Error aggregation test

* `Validate_WithMultipleViolations_ThrowsConfigurationExceptionWithAllErrors`

  * Build a config that violates several rules at once (e.g. duplicate connection names + job with missing tables).
  * Assert that `ConfigurationException.Errors` has all expected codes.

---

## 5. Wiring into the Configuration Pipeline

Although the main responsibility is in Task 2.3, we should define how the validator fits into the load flow so other coders don’t improvise later.

### 5.1 Configuration service / facade

Assuming there is (or will be) a service like `IConfigurationProvider` that loads YAML and returns a `SyncConfiguration`:

```csharp
public interface IConfigurationProvider
{
    Task<SyncConfiguration> LoadAndValidateAsync(string configPath, CancellationToken cancellationToken = default);
}
```

Implementation should:

1. Use YAML loader from Task 2.2 to obtain `SyncConfiguration`.
2. Invoke `IConfigurationValidator.Validate(config)`.
3. Return `config` if no exception.

Validator itself is stateless and can be registered as singleton.

### 5.2 DI registration (later section coordination)

In `ReportSyncer.Core` (or in a dedicated `ReportSyncer.Core.Configuration.Extensions` class):

```csharp
public static class ConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddReportSyncerConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
        // services.AddSingleton<IConfigurationProvider, YamlConfigurationProvider>(); // from Task 2.2
        return services;
    }
}
```

---

## 6. Step-by-step Implementation Checklist

1. **Introduce error types (if not already created)**

   * `ConfigurationError` (immutable record).
   * `ConfigurationException` with `IReadOnlyList<ConfigurationError> Errors`.
2. **Create `ConfigurationErrors` helper** with factory methods + error codes.
3. **Add `IConfigurationValidator` interface`** in `ReportSyncer.Core.Configuration`.
4. **Implement `ConfigurationValidator`**:

   * Root method `Validate` with `List<ConfigurationError>` and `ThrowIfAny`.
   * Private methods: `ValidateRoot`, `ValidateConnections`, `ValidateJobs`, `ValidateTables`, `ValidateSafety`.
   * Helper methods: `JobAllowsProdToProd`, `TableAllowsFullDelete`, `ValidateTableDeleteSettings`.
5. **Implement validation rules** exactly as per PRD, filling in safety logic placeholders.
6. **Write unit tests** under `Tests/ReportSyncer.Core.Tests/Configuration/ConfigurationValidatorTests.cs`.

   * Ensure coverage of positive and negative scenarios.
7. **Wire validator into configuration pipeline** (used by YAML loader / provider) so that hosts never see unvalidated configs.
8. **Document behavior** via XML docs on `IConfigurationValidator` and `ConfigurationException` so future host code knows how to interpret errors.

Once these steps are done, Task 2.3 is effectively complete and Tasks 2.4 / 2.5 have clear extension points to plug override logic and host-facing error mapping on top of this validator.
