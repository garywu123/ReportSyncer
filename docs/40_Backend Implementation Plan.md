**Reference Documents:**

- `10_Product Requirements Document (PRD).instruction.md` (Product Behavior)
- `30_ReportSyncer Backend Arch.instruction.md` (Architecture, Classes, Interfaces)
- `20_ReportSyncer Backend Project Instruction.instruction.md` (Project General Arch)

## Section 1: DotNetToolkit – Hardening & NuGet Packaging

**Project focus:**

- `DotNetToolkit.General` (Guard, Result, general helpers)
    
- `DotNetToolkit.Logging` (ILogService + Serilog adapter)
    
- `DotNetToolkit.Database` (IDbContext, IDbConnectionFactory, IDbCommandWrapper, IDataMapper, DbContext, DbCommandWrapper, ReflectionDataMapper, DI extensions)
    

**High-level goal**  
Turn DotNetToolkit into a clean, domain-agnostic infra library, packaged as NuGet(s), that ReportSyncer.Core consumes but never pollutes with sync-specific logic. This must obey the strict rules that `DotNetToolkit.*` never depends on `ReportSyncer.*` and stays reusable.

### Global principles for this section

- AI coder **must**:
    
    - Treat `DotNetToolkit.*` as **domain-agnostic infra**:
        
        - No mention of ReportSyncer, AGV, sync jobs, safety flags, YAML, etc. in public API or internals.
            
    - Keep technology stack within:
        
        - `Microsoft.Extensions.*`, `Microsoft.Data.SqlClient` in Database.
            
        - No EF, no ORMs, no Dapper in toolkit.
            
    - Design the public APIs so they can be consumed by any .NET 9 app, not just ReportSyncer.
        
- AI coder **may**:
    
    - Add small helper types (retry, tiny functional helpers) if they are generic.
        
    - Adjust class names & namespaces slightly to improve clarity, as long as:
        
        - No breaking change to what ReportSyncer will need (IDbContext, IDbCommandWrapper semantics, etc. match the backend architecture docs).
            
- AI coder **must not**:
    
    - Introduce any dependency on:
        
        - `ReportSyncer.Core`, `ReportSyncer.Console`, `ReportSyncer.WebApi` or domain exception types.
            
    - Implement sync-specific SQL (TableTask, preSyncTargetAction, identity insert policy, safety rules) in `DotNetToolkit.Database`. Those belong in `ReportSyncer.Core.Sync`.
        

---

### Task 1.1 – Finalize DotNetToolkit.General (Guard & Result)

**Goal**  
Stabilize `DotNetToolkit.General` as a small, boring but solid general-utilities library:
- Guard helpers for argument validation.
- A simple `Result<T>` type for generic operations where ReportSyncer (and others) may want to avoid exceptions in some cases.

**Inputs**
- Source files:
    - `DotNetToolkit.General/Guard.cs`
    - `DotNetToolkit.General/Result.cs`
- Backend architecture docs for allowed toolkit responsibilities.
    
**What must be implemented**

- Review and finalize:
    - `Guard.NotNull`, `Guard.NotNullOrEmpty`:
        - Ensure consistent exception types (`ArgumentNullException`, `ArgumentException`) and parameter naming.
    - `Result<T>`:
        - Keep `Ok` / `Fail` factory methods.
        - Ensure it is immutable from consumers’ perspective.
- Add minimal additional helpers only if they’re truly generic:
    - Example: `Guard.InRange`, `Guard.NotDefault<T>`, etc., **only** if they don’t encode ReportSyncer rules.

**Constraints & freedom**
- No extra dependencies; `DotNetToolkit.General` must stay ultra-lean.
- Do **not** add any logging, configuration or DB awareness here.
    

**Expected behavior / tests**
- Unit tests in `DotNetToolkit.Tests`:
    - `Guard_NotNull_WithNull_ThrowsArgumentNullException`
    - `Result_Ok_IsSuccessTrueAndValueSet`
    - `Result_Fail_IsSuccessFalseAndErrorSet`

---

### Task 1.2 – DotNetToolkit.Logging: ILogService + Serilog Adapter

**Goal**  
Provide a clean logging abstraction (`ILogService`) plus a Serilog-backed implementation, suitable for any app, including ReportSyncer.

**Inputs**

- `DotNetToolkit.Logging/ILogService.cs` and project file.
- Backend docs for logging requirements:
    - Core depends on `DotNetToolkit.Logging.ILogService`, hosts wire Serilog.
        

**What must be implemented**
- Implement `SerilogLogService` (name can vary slightly) that:
    - Wraps a Serilog `ILogger` instance.
    - Implements all `ILogService` sync/async methods:
        - `LogVerbose`, `LogDebug`, `LogInformation`, `LogWarning`, `LogError`, `LogCritical` (+ async equivalents).
- Add DI registration helper:
    - E.g. `LoggingServiceCollectionExtensions.AddSerilogLogService(this IServiceCollection services, ILogger serilogLogger)`.
- Ensure logging abstraction remains generic (no domain terms).
    

**Constraints & freedom**
- `DotNetToolkit.Logging` may reference Serilog packages (as allowed infra), but cannot reference ReportSyncer types.
- Do not bake any file paths, config keys or environment logic inside the library; hosts decide.
    
**Expected behavior / tests**
- Unit tests with a fake Serilog logger:
    - Verify each `ILogService` method calls the correct Serilog level.
- Confirm ReportSyncer can depend **only** on `ILogService` without knowing Serilog exists.
    

---

### Task 1.3 – DotNetToolkit.Database: Abstractions & Implementations

**Goal**  
Harden `DotNetToolkit.Database` as the canonical low-level data-access abstraction:
- Stable interfaces: `IDbConnectionFactory`, `IDbContext`, `IDbCommandWrapper`, `IDataMapper<T>`.
- SQL Server implementation using `Microsoft.Data.SqlClient` only.
    

**Inputs**
- Repo files:
    - `Abstractions/*.cs` (`IDbContext`, `IDbConnectionFactory`, `IDbCommandWrapper`, `IDataMapper<T>`)
    - `Configuration/DatabaseSettings.cs`
    - `Internal/DbCommandWrapper.cs`, `Internal/ReflectionDataMapper.cs`
    - `Services/DbConnectionFactory.cs`, `Services/DbContext.cs`
    - `Extensions/DatabaseServiceCollectionExtensions.cs`
- Backend architecture stating `DotNetToolkit.Database` responsibilities & constraints.
    

**What must be implemented / fixed**

1. **Align interfaces & implementations**
    - Ensure `IDbConnectionFactory.CreateConnectionAsync(...)` is implemented in `DbConnectionFactory` consistent with `CreateConnection()`:
        - Use `SqlConnection` from `Microsoft.Data.SqlClient`.
        - Open connection asynchronously when requested.
    - Ensure `IDbContext` implementation (`DbContext`) consistently:
        - Uses the factory.
        - Sets `CommandTimeout` from `DatabaseSettings.CommandTimeoutSeconds`.
            
2. **DbCommandWrapper robustness**
    
    - Ensure `DbCommandWrapper`:
        
        - Always creates parameters via `DbProviderFactory`.
            
        - Correctly sets `DbType`, `Direction`, null handling, and conversion on `GetParameterValue<T>`.
            
3. **ReflectionDataMapper**
    
    - Make sure mapping:
        
        - Resolves properties case-insensitively (already implemented).
            
        - Safely skips unmapped columns.
            
    - No domain logic, just “column name → property name” mapping.
        
4. **DatabaseSettings & provider selection**
    
    - Confirm `DatabaseSettings` only has:
        
        - `ConnectionString`, `ProviderName`, `CommandTimeoutSeconds`.
            
    - Keep provider support constrained:
        
        - For now, support only `Microsoft.Data.SqlClient` (as your stack requires).
            

**Constraints & freedom**

- No ReportSyncer-specific SQL allowed here:
    
    - No `TableTask`, no delete policies, no context column, no identity insert rules.
        
- All async methods must accept `CancellationToken` properly but must **not** throw domain-specific exceptions; only generic .NET exceptions.
    

**Expected behavior / tests**

- **Unit tests**:
    
    - `DbCommandWrapper_AddParameter_StoresParameterWithCorrectTypeAndDirection`.
        
    - `ReflectionDataMapper_MapsColumnsToProperties_IgnoringCase`.
        
- **Integration tests** against LocalDB or test SQL Server:
    
    - `DbConnectionFactory_CreateConnection_ConnectsWithConfiguredConnectionString`.
        
    - `DbContext_ExecuteNonQueryAsync_ExecutesCommand`.
        
    - `DbContext_ExecuteQueryAsync_MapsRowsToObjectsUsingReflectionDataMapper`.
        

---

### Task 1.4 – DI Extensions & Multi-project Consumption

**Goal**  
Provide clean DI hooks so any host (ReportSyncer.Console, ReportSyncer.WebApi, or other apps) can register toolkit services via `Microsoft.Extensions.DependencyInjection`.

**Inputs**

- `DotNetToolkit.Database/Extensions/DatabaseServiceCollectionExtensions.cs`
    
- Logging plan from Task 1.2.
    

**What must be implemented**

- Validate and, if needed, refine `AddDatabaseServices`:
    
    - Binds `DatabaseSettings` from configuration section.
        
    - Registers:
        
        - `IDbConnectionFactory` as singleton.
            
        - `IDbContext` as scoped.
            
        - `IDataMapper<>` as transient (ReflectionDataMapper).
            
- Optionally add parallel DI helpers for Logging:
    
    - `AddLoggingServices` to wire `ILogService` to Serilog adapter.
        

**Constraints & freedom**

- Keep extension methods in toolkit projects, not in ReportSyncer projects.
    
- Do not assume any specific configuration keys beyond logically named sections (e.g. `"DatabaseSettings"` here is acceptable, but no “ReportSyncer” naming).
    

**Expected behavior / tests**

- Unit tests with a dummy `IConfiguration`:
    
    - `AddDatabaseServices_BindsDatabaseSettingsAndRegistersServices`.
        
- Sanity integration test:
    
    - Build a `ServiceProvider`, resolve `IDbContext` and run a trivial select.
        

---

### Task 1.5 – Test Project & NuGet Packaging

**Goal**  
Make DotNetToolkit a “real” library: tested and packable.

**Inputs**
- `Tests/DotNetToolkit.Tests` project.
- Tech stack & testing libraries (`xUnit`, etc.) from architecture docs.
    
**What must be implemented**
- Expand `DotNetToolkit.Tests`:
    - Organize tests into folders/namespaces:
        - `General`, `Logging`, `Database.Unit`, `Database.Integration`.
- Add basic integration test infra for SQL:
    - Connection string configuration for LocalDB / dev SQL.
    - Fixtures to set up and tear down simple test tables.
        
- Update each `.csproj` for toolkit projects:
    - Fill in `PackageId`, `Authors`, `Description`, `RepositoryUrl`.
    - Configure `GeneratePackageOnBuild` or create a shared pack script.
        
- Ensure versioning scheme is clear (e.g. 0.1.x pre-release) and not tied to ReportSyncer version directly.
    
**Constraints & freedom**
- Packaging is **library**-level:
    - No dependency from toolkit `.csproj` to ReportSyncer projects.
- Tests may reference any test library (`xUnit`, `FluentAssertions`, `Moq`), but these stay in test project only.
    

**Expected behavior / tests**
- `dotnet test` on DotNetToolkit.Tests passes.
- `dotnet pack` on toolkit projects produces `.nupkg` files with correct metadata.
- ReportSyncer solution can reference the projects directly **or** consume the NuGet packages without API changes.
    

---

## Section 2 (Revised): Configuration & Error Model – Implementation Plan

Namespace focus: `ReportSyncer.Core.Configuration` (+ shared domain error types)

Important note in light of Section 1:

- This section **uses** toolkit projects (`DotNetToolkit.General`, `DotNetToolkit.Logging`, `DotNetToolkit.Database` where appropriate) but **must not** push any domain logic down into them. All config & error semantics stay in `ReportSyncer.Core`.
    

### Global principles for this section

- AI coder **may**:
    
    - Introduce helper classes, extension methods, small patterns (factory, value objects, etc.).
    - Add derived safety checks or convenience methods if consistent with the PRD & architecture doc.
        
- AI coder **must not**:
    - Bypass the validation layer and use raw YAML objects in sync logic.
    - Put DB access, logging, or orchestration in this namespace.
    - Push configuration error types into `DotNetToolkit.*`; domain exceptions live in `ReportSyncer.Core` only.
        
- AI coder **must**:
    - Read and respect:
        - PRD: config structure, safety rules, delete behavior, environment rules.
        - Backend architecture doc: configuration responsibility, layering rules, and exception taxonomy.
            
_(Everything below is your existing Section 2 content, kept intact in meaning; I’m not rewriting the world, just making it fit the new DotNetToolkit split.)_

---

### Task 2.1 – Define the Configuration Domain Model

**Goal**  
Represent the YAML configuration as a strongly-typed, immutable-ish domain model that:
- Captures run settings, safety rules, DB connections, sync jobs, table tasks, and filters.
- Is expressive enough to support all behaviors in the PRD, including safety features around deletes and prod/prod scenarios.
    

**Inputs (docs to read first)**
- PRD: sections describing:
    - The YAML structure (root config, connections, sync jobs, table-level options).
    - Safety options (forbid prod→prod, large delete thresholds, allow-all-delete style flags).
        
- Backend architecture doc:
    - Configuration responsibilities & layering.
    - Exception taxonomy (how `ConfigurationException` fits in).
        
**What must be implemented**
- A **root configuration object** representing the whole YAML file:
    - Holds:
        - Runtime options (dry-run, batch sizes, etc.).
        - Safety-related settings.
        - A list of connections.
        - A list of sync jobs, each with table-level instructions.
- Job-level and table-level configuration objects that:
    - Identify which source/target connections to use.
        
    - Identify source and target tables.
        
    - Capture pre-sync actions (like delete target / filter-based delete).
        
    - Capture any filters or context-related hints needed by schema/mapping.
        
- Connection configuration objects that:
    
    - Represent named connections.
        
    - Carry environment information (Prod/Dev/etc.).
        
    - Indicate DB type so later layers can pick appropriate providers (Application vs Reporting).
        
- Filter-related objects that:
    
    - Represent column filters in a structured way (column, operator, value(s)), not raw string SQL.
        
- A single, central place in this section for **config-related domain types**, not scattered across sync & infra.
    

**Constraints & freedom**

- Structures must align logically with YAML examples in the PRD but:
    
    - AI coder can introduce intermediate types (e.g. value objects, enums, wrapper types) to improve clarity and safety.
        
    - Names can be slightly adjusted if they better express the intent, as long as docs & YAML are respected.
        

**Expected behavior / tests (concept level)**

- Given a valid YAML (per PRD), the domain model can represent:
    
    - Multiple connections with different envs.
        
    - Multiple jobs with multiple tables.
        
    - Per-table safety / pre-sync behavior where applicable.
        
- Creating configuration objects with obviously invalid state (e.g. empty names, missing required parts) is either:
    
    - Prevented by type design, or
        
    - Reliably caught by validation (Task 2.3).
        

---

### Task 2.2 – Implement Configuration Loading (YAML → Domain)

**Goal**  
Transform a YAML configuration file into the domain model from Task 2.1 with:

- Strict parsing.
- Clear failure on malformed or unknown content.
- No semantic validation yet. Just structural correctness.
    
**Inputs (docs)**
- PRD:
    - YAML examples / schema description.
    - Any notes on future-proofing / backward compatibility.
- Backend architecture doc:
    - Host responsibilities vs core responsibilities (where file I/O ends, where core starts).
        
**What must be implemented**
- An abstraction for loading configuration from a file path (or stream) into the domain model.
- A YAML-based implementation that:
    - Reads from disk (or host-provided stream).
    - Maps YAML structure to the model types defined in Task 2.1.
    - Fails fast on:
        - Syntax errors.
        - Unknown top-level or nested fields.
        - Unmappable types (e.g. wrong primitive types).
            

**Constraints & freedom**
- Use `YamlDotNet` as the only YAML library, per library requirements.
- AI coder can:
    - Use DTOs + mapping OR direct mapping to domain model, as long as the end result is strongly-typed.
    - Add helper classes to track config path / location for better error messages.
        
- All errors at this stage must surface as **configuration-layer failures**, not generic runtime exceptions, so that host can map them later into `ConfigurationException`.
    

**Expected behavior / tests**
- Valid YAML sample from PRD loads into the domain model with all expected data present.
- When the YAML structure is broken (indentation, missing list markers, etc.), a configuration-layer exception is thrown with:
    - Clear indication it’s a parse problem.
- When unknown keys appear in YAML, loading fails with:
    - Explicit message that the key is unsupported (not silently ignored).
        
#### Task 2.2.1 Unit tests for YAML loader (in-memory)

**Class:** `YamlConfigurationLoaderTests`

| Test method name                                                                                              | Why test this                                                                                                                                       | Expected result                                                                                                                                                                                                   |
| ------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LoadAsync_WithValidMinimalConfig_ReturnsExpectedSyncConfiguration`                                           | Prove that the smallest valid YAML config maps correctly into `SyncConfiguration` and nested types.                                                 | Method returns successfully; `SyncConfiguration` is not null; counts for `Connections`, `SyncJobs`, `Tables` match YAML; key properties are mapped as expected; optional sections use defaults/nulls as designed. |
| `LoadAsync_WithFullExampleConfig_ReturnsExpectedSyncConfiguration`                                            | Ensure the loader handles a realistic “full” config with all sections populated (run/safety/schemaPolicy/multiple connections/jobs/tables/filters). | Method returns successfully; all sections populated; values for safety, schema policy, environments, connection types, job parameters, and table/filter settings match the YAML sample.                           |
| `LoadAsync_WithUnknownTopLevelKey_ThrowsConfigurationException`                                               | Enforce fail-fast behavior for unknown root-level fields so configs don’t silently drift.                                                           | Passing YAML with an extra root property throws `ConfigurationException`; message indicates unknown field or similar, ideally naming the offending key.                                                           |
| `LoadAsync_WithUnknownNestedKey_ThrowsConfigurationException`                                                 | Same principle as above, but for nested objects (tables, filters, jobs, etc.).                                                                      | Passing YAML with an unsupported nested property throws `ConfigurationException`; message indicates unknown field and context (e.g. within `tables` or `filters`).                                                |
| `LoadAsync_WithWrongPrimitiveType_ThrowsConfigurationException`                                               | Ensure type mismatches (string where int/decimal/bool expected) are surfaced clearly, not as random runtime errors.                                 | YAML with wrong primitive type (e.g. `"eighty"` for an integer) throws `ConfigurationException`; message references the field and type problem.                                                                   |
| `LoadAsync_WithInvalidSchemaPolicyValue_ThrowsConfigurationException`                                         | Guard enum-like fields (`schemaPolicy` etc.) against invalid strings.                                                                               | YAML with invalid schema policy (or similar enum field) throws `ConfigurationException`; message indicates invalid value and allowed options or field name.                                                       |
| `LoadAsync_WithInvalidYamlSyntax_ThrowsConfigurationException`                                                | Verify corrupt YAML is mapped into a clean configuration error instead of raw parser exceptions.                                                    | Badly formatted YAML causes `ConfigurationException`; message indicates a YAML parse issue (not just a low-level `YamlException`).                                                                                |
| `LoadAsync_WithEmptyYaml_ThrowsConfigurationException`                                                        | Handle the “file exists but empty” case predictably.                                                                                                | Empty YAML input throws `ConfigurationException`; message indicates missing required root configuration / sections.                                                                                               |
| `LoadAsync_WithParameterPlaceholders_ProducesExpectedModel` _(optional, only if loader handles placeholders)_ | Lock in how parameter placeholders like `{StartDate}` / `{CustomerId}` are represented after loading.                                               | Loader either preserves placeholders as-is in model or resolves them according to your design; resulting `SyncConfiguration` matches the expected representation of placeholders.                                 |

---

#### Task 2.2.2 – Integration tests for YAML loading (file IO)

**Class:** `ConfigurationLoadingIntegrationTests`

| Test method name                                                               | Why test this                                                                                                | Expected result                                                                                                                                                                              |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LoadAsync_WithValidMinimalConfigFile_LoadsSuccessfully`                       | Prove that a minimal, on-disk YAML file goes through real file IO + YAML parsing and returns a valid config. | Given `sync_valid_minimal.yaml` on disk, loader completes without exception; `SyncConfiguration` is non-null; basic counts and key values match the file.                                    |
| `LoadAsync_WithValidFullConfigFile_LoadsAllSections`                           | End-to-end check that a realistic full config file loads correctly with IO + parsing + mapping.              | Given `sync_valid_full.yaml`, loader completes without exception; all sections (run, safety, schema, connections, jobs, tables, filters) are populated and match the YAML contents.          |
| `LoadAsync_WithConfigFileContainingUnknownKey_ThrowsConfigurationException`    | Ensure unknown keys in a real file are rejected with a clear error instead of being ignored.                 | Given `sync_invalid_unknown_key.yaml` with extra field(s), call throws `ConfigurationException`; message mentions unknown field / invalid configuration and ideally includes the field name. |
| `LoadAsync_WithConfigFileContainingInvalidPolicy_ThrowsConfigurationException` | Validate real-file behavior when policy-like values are invalid (schema policy, safety mode, etc.).          | Given `sync_invalid_schema_policy_value.yaml`, call throws `ConfigurationException`; message indicates invalid value and which setting failed.                                               |
| `LoadAsync_WithConfigFileContainingBadYaml_ThrowsConfigurationException`       | Verify corrupt YAML on disk is reported clearly to the caller.                                               | Given `sync_invalid_syntax.yaml`, call throws `ConfigurationException`; message points to YAML parse failure and includes the file path or enough context for troubleshooting.               |
| `LoadAsync_WithNonExistingFilePath_ThrowsConfigurationException`               | Ensure missing files become a clean configuration error that higher layers can show to the user.             | Passing a non-existent path throws `ConfigurationException`; message includes the missing path; no raw `FileNotFoundException` leaks out of the loader boundary.                             |

You can drop these tables straight into the implementation plan under Task 2.2.1 / 2.2.2.

And yes, keep these tests **before** 2.3: first prove “can I load anything sane at all,” then layer “is this config allowed by business rules” on top.

---

### Task 2.3 – Implement Configuration Validation

**Goal**  
Check all **static** rules that can be validated without DB access:

- Structural integrity.
- Reference integrity (connections referenced by jobs exist).
- Safety-related configuration rules.
- “Obvious footgun” prevention strictly based on config + PRD rules.
    

**Inputs (docs)**
- PRD: rules for:
    - Environment combinations (e.g. prod→prod restrictions).
    - Requirements around destructive operations (table deletes, etc.).
    - Thresholds / latches (large delete percentages, explicit allow flags).
        
- Backend architecture doc:
    - Description of pre-flight / configuration validation responsibilities.
    - Relationship with safety module and schema module.
        

**What must be implemented**
- A **validator component** that accepts the domain model and either:
    - Returns cleanly (valid), or
    - Throws configuration-layer exception(s) listing all violations.
        
- Validation rules that cover at least:
    - Required fields presence.
    - Unique connection names.
    - Jobs only referencing defined connections.
    - Each job having at least one table task.
    - Safety flags consistent with destructive operations.
    - Numeric ranges (e.g. delete thresholds between 0 and 1 if that’s the spec).
        
- Capability to report **where** the error occurred (job name, table index, etc.) so host / user can fix YAML quickly.
    

**Constraints & freedom**

- AI coder may:
    - Aggregate multiple validation errors into one exception with a collection payload.
    - Introduce helper types like `ConfigurationError` to structure errors.
    - Add additional “obvious” checks if they align with business expectations (e.g. disallow zero batch sizes, whitespace-only names).
        
- Validator must NOT:
    - Do schema inspection.
    - Connect to DB.
    - Apply runtime-derived rules.
        

**Expected behavior / tests**
- Valid configuration from PRD passes validation.
- Broken references (unknown connection names, empty jobs, etc.) produce clear error messages.
- Full-table destructive behavior without explicit safety latch from PRD produces a clear config error.
- Multiple config issues in one file can be surfaced together where practical.
    

---

### Task 2.4 – Build Effective Configuration / Overrides Logic

**Goal**  
Allow hosts (CLI/Web API) to override certain runtime options (like dry-run, batch sizes) without corrupting or rewriting the original configuration:

- Compute an **effective runtime view** based on:
    - YAML config.
    - Optional host-provided overrides.
        
**Inputs (docs)**
- PRD:
    - Which fields should be “user-tunable at run-time” vs fixed in config.
- Backend architecture doc:
    - How hosts are expectd to influence runs (CLI arguments, API payloads).
        
**What must be implemented**
- A clear concept of:
    - “Base configuration“: what comes from the YAML.
    - “Overrides”: limited set of host-supplied tweaks.
    - “Effective configuration”: the immutable runtime view after merge.
        
- Merge rules:
    - Override wins if present.
    - Otherwise use YAML.
    - If neither exists but the feature requires a value, either:
        - Use a documented default, or
        - Fail with a configuration-layer error, depending on PRD.
            
**Constraints & freedom**
- AI coder can:
    - Decide exact object shapes for overrides and effective configuration.
    - Factor repeated merge logic into helpers.
        
- Overrides must be localized:
    - No scattered “manual overrides” sprinkled across orchestrator / sync pipeline.
        
**Expected behavior / tests**
- When overrides are given, the effective values reflect the overrides and nothing else is touched.
- When overrides are omitted, the effective values reflect YAML.
- It is impossible for later layers (sync, schema) to accidentally “partially see” overrides.
    



---

### Task 2.5 – Configuration Error Model & Host Mapping Contract

**Goal**  
Have a **single, explicit error model** for configuration failures, and a clear contract for how hosts react to them (exit codes / HTTP statuses):

- Make configuration-related errors recognizable and consistent.
    
- Avoid random generic exceptions leaking out of the core.
    

**Inputs (docs)**

- Backend architecture doc:
    
    - Domain exception taxonomy.
        
    - Mapping rules to host behavior (console/Web API).
        

**What must be implemented**

- A dedicated configuration error abstraction, e.g.:
    
    - A configuration-specific exception type (name per architecture).
        
    - Optionally supporting:
        
        - A path or location hint in the config.
            
        - A list of structured validation errors.
            
- Clear internal usage:
    
    - Loader throws this type for parse / mapping issues.
        
    - Validator throws this type for semantic issues.
        
    - Other config-related code (overrides, effective config) uses the same type for config-level problems.
        
- A **documented** (in comments / XML docs / summary) mapping contract:
    
    - Console host: maps configuration errors to specific exit code and error logging pattern.
        
    - Web API host: maps configuration errors to HTTP 4xx with structured payload.
        

**Constraints & freedom**

- AI coder can:
    
    - Decide whether to store multiple errors, nested errors, or simple message-only in v1, as long as:
        
        - It’s testable.
            
        - It doesn’t break the mapping contract.
            
- Error messages should be user-oriented, not “internal stack trace dump”.
    

**Expected behavior / tests**

- Any invalid configuration path (parse, validate, override) surfaces as the domain configuration error type.
    
- Host-layer tests (later sections) can reliably detect and map configuration errors via this type.
    

---

That’s the plan.

You’ve cleanly separated:

- **Section 1:** Infra/toolkit (DotNetToolkit) as independent NuGet(s).
    
- **Section 2+:** ReportSyncer.Core domain behavior that _uses_ the toolkit.
    

This matches your “assign different AI coders per section” model and keeps future you from hunting domain logic inside some “generic helpers” library at 2 a.m.


## Section 3: Schema & Dependency Subsystem (`ReportSyncer.Core.Schema`)

> Using the PRD and backend architecture docs, design an **implementation plan** for the “Schema & Dependency Subsystem” in `ReportSyncer.Core.Schema`.
> 
> Scope of this section:
> - Inspect source and target schemas for the configured tables.
> - Represent tables, columns, keys, and relationships in the domain model.
> - Implement schema mapping (source→target), including handling of context columns described in the PRD.
> - Build a dependency graph between tables to determine a safe execution order (respecting FK relationships, etc.).
> - Validate that, for each sync job, the **selected Target tables** (i.e., the tables with `TableTask` entries) form a **dependency-closed set**:
>     - If any selected table has a foreign key to another Target table that is not selected for the job, pre-flight must fail with a clear error listing the missing tables and which selected tables depend on them.


> Your output should be:
> 
> 1. A list of **implementation tasks** for schema and dependency handling.
> 2. For each task:
>     
>     - Which domain models / interfaces are needed (schema models, inspectors, mappers, dependency resolvers) and what behaviors they must support.
>         
>     - How schema information is obtained from SQL Server (or other DBs if allowed later) using the infrastructure / DotNetToolkit layer instead of ad-hoc SQL sprinkled everywhere.
>         
>     - How mapping rules and dependency rules must reflect the business rules and safety considerations described in the PRD.
>     - In each task, you should have a list of steps, for the function/fields/unit tests that we would need to apply. 
>         
> 3. For each task, define **tests**:
>     
>     - Simple schema with 1–2 tables.
>     - Schema with foreign keys requiring ordered execution.
>     - Mismatched schemas leading to clear, actionable errors (e.g. missing columns, incompatible types).
>     - A job that selects a subset of Target tables where one table's foreign key points to an unselected Target table:
>         - Expected: dependency resolution / pre-flight fails with a schema/dependency error that:
>             - Names the selected table(s) with invalid dependencies.
>             - Lists the missing Target table(s) that must be added to the job (or the dependent tables removed).
> Explicitly reference how this section feeds into the **pre-flight validation** and **sync execution pipeline** in later sections.

好，你要当 code manager，我来帮你把 section 3 拆成可以丢给 AI coder 的任务清单。
不写流水账步骤，但每个 task 都是「能实现、能测、边界清楚」的粒度。

下面所有实现都在：

* **Project**：`ReportSyncer.Core`
* **Namespace 主线**：`ReportSyncer.Core.Schema`
* 对应测试在 `ReportSyncer.Tests` 下按模块分文件夹，比如 `Schema`, `Schema.Mapping`, `Schema.Dependency` 等。

---

### Task 3.1：定义 Schema 领域模型（纯内存结构）

**目标**
建立与数据库解耦的 schema 领域模型，为后面的 inspector / mapper / dependency 做基础。

**主要产物**

* 新增基础类型（示例命名，可调整，但风格要统一）：
  * `TableIdentifier`：`SchemaName`, `TableName`（用于字典 key / 比较）
  * `ColumnSchema`：`Name`, `ClrType` / `DbType`, `IsNullable`, `IsIdentity`, `IsPrimaryKeyPart`, `MaxLength` 等
  * `ForeignKeySchema`：`Name`, `FromTable`, `ToTable`, `ColumnPairs`（`FromColumn` → `ToColumn`），`IsCascadeDelete`
  * `TableSchema`：`Table`, `Columns`, `PrimaryKeyColumns`, `ForeignKeys`, `Indexes`（如果 PRD/Arch 有要求）
  * `SchemaSnapshot`：`Tables`（按 `TableIdentifier` 索引），`SourceOrTargetRole`（用于区分源/目标）
* 基本行为约束：
  * 所有类型不可变或「construction 完成后只读」。
  * 合理实现 `Equals` / `GetHashCode`（至少对 `TableIdentifier`）。
  * 方便后面用 LINQ 过滤、构造依赖图。
**测试要点**

* 构造几个手工 `SchemaSnapshot`，验证：
  * `TableIdentifier` 区分 schema + name（`dbo.Customer` ≠ `report.Customer`）。
  * 能正确标识 PK / FK / Nullable 等信息。
  * 集合结构方便用作 dictionary key / lookup，不出幺蛾子（比如 hash 不一致之类）。

---


### Task 3.2：实现 Schema Inspection 抽象与 SQL Server 实现

**目标**
通过 `DotNetToolkit.Database` 从 SQL Server 读取 schema，映射到 Task 3.1 的领域模型。

**主要产物**

* 接口 `ISchemaInspector`（在 `ReportSyncer.Core.Schema`）：

  * 典型签名类似：

    ```csharp
    Task<SchemaSnapshot> InspectAsync(
        ConnectionConfig connection,
        IEnumerable<TableIdentifier> tables,
        CancellationToken ct);
    ```
  * 只关心「本次 sync 需要的表」，不扫整个 DB。
* 实现类 `SqlServerSchemaInspector`：

  * 使用 `DotNetToolkit.Database` 的 `IDbContext` / `DbCommandWrapper` 之类去访问：

    * 列信息、主键、外键、数据类型（只取 PRD & Arch 需要的字段）
  * 按照 `TableIdentifier` 构造 `TableSchema` / `ColumnSchema` / `ForeignKeySchema`。
  * 对不存在的表给出清晰错误（区分“连接不通”和“表不存在”）。
* 与 Config 的连接：

  * 明确 ISchemaInspector 不直接依赖 YAML，只依赖上层传下来的 `ConnectionConfig` 和 `TableIdentifier` 列表。

**测试要点**

* 单元测试（可以用 mock `IDbContext` / `IDbCommandWrapper`）：

  * 返回简单结果集，验证能构成正确的 `SchemaSnapshot`。
* 集成测试（LocalDB / 测试数据库）：

  * 建一个 tiny schema（2–3 表 + FK），调用 `InspectAsync`：

    * 所有表与列都被发现。
    * FK 方向正确，PK 列识别正确。
  * 错误路径：

    * 指定一个不存在的表名，返回可读错误信息，而不是莫名其妙的 SQL 异常泄露到底层。

---

### Task 3.3：实现 Schema Mapping（源 → 目标）与上下文列规则

**目标**
在已有 `SchemaSnapshot` 基础上，为每个 `TableTask` 建立源表与目标表的列映射，处理 PRD 里 Application→Reporting 的 **Context Column**（默认 `CustomerId`）等业务规则。

**主要产物**

* 新的 mapping 领域对象（示例）：

  * `ColumnMapping`：`SourceColumn?`, `TargetColumn`, `MappingKind`（OneToOne / Constant / ContextColumn / Ignored）
  * `TableMapping`：`SourceTable`, `TargetTable`, `ColumnMappings`, `PrimaryKeyMapping`, 兼容性 flags 等
  * `SchemaMappingResult`：`Success` / `Failure` + 错误列表
* 类 `SchemaMapper`：

  * 输入：

    * 源 `SchemaSnapshot`、目标 `SchemaSnapshot`
    * `SyncConfiguration` 或至少当前 `SyncJob` + `TableTask` 列表
  * 行为：

    * 对每个 `TableTask` 找到对应的源/目标 `TableSchema`。
    * 自动做「按列名匹配」的一对一映射，忽略大小写策略与 Arch 保持一致。
    * 应用 PRD 里的 Context Column 规则：

      * `Source.Type == Application && Target.Type == Reporting` 时，自动在目标侧补上 Context Column：

        * 从 job parameter 中取值，生成 `ColumnMapping`（MappingKind = ContextColumn）。
        * 如果目标已经有同名列且来源有值，触发“歧义保护”错误。
    * 做基础类型兼容性检查（按类型家族，nvarchar ↔ varchar / int ↔ bigint 等）。
    * 对缺失必需列、类型不兼容，生成结构化错误（含表名、列名、源/目标类型）。
* 与安全相关：

  * 不在这里做“是否允许同步该表”的安全判断，只做「结构是否能安全映射」。

**测试要点**

* 纯内存测试，不依赖真实 DB：

  * 简单 1–2 表场景：完美匹配，`SchemaMappingResult.Success == true`。
  * 缺失目标列 / 源列，得到清晰错误，含具体表/列名。
  * Application → Reporting 场景：

    * 目标无 `CustomerId`：应自动生成 Context Column mapping。
    * 目标有 `CustomerId`，源也有 `CustomerId` 且 job parameter 也提供：触发歧义错误。
  * 类型不兼容：例如源 `nvarchar(50)` → 目标 `int`，返回错误。

---

### Task 3.4：实现 FK 依赖图与执行顺序解析（IDependencyResolver）

**目标**
在目标 schema 上构建 FK 依赖图，计算插入 / 删除顺序，并检测循环依赖。

**主要产物**

* 领域对象：

  * `TableDependencyNode`：对应一个目标表，包含依赖的父表 / 子表列表。
  * `DependencyGraph`：节点集合 + 边集合。
  * `DependencyPlan`：`InsertOrder`（拓扑序）和 `DeleteOrder`（反向拓扑序）。
* 接口 `IDependencyResolver`：

  * 输入：目标 `SchemaSnapshot`、本 job 参与的目标表集合（从 `TableTask` 推出）。
  * 输出：`Result<DependencyPlan>`（或项目里统一的 `Result<T>`），包含拓扑排序结果或错误。
* 实现：

  * 构图：

    * 每个 FK：`ChildTable` 依赖 `ParentTable`。
  * 拓扑排序：

    * 插入顺序：父 → 子。
    * 删除顺序：子 → 父。
  * 检测循环：

    * 如果有环（比如自引用或多表互相引用），返回「包含环的路径」，而不是模糊一句“cycle detected”。

**测试要点**

* 单元测试（纯内存）：

  * 无 FK：执行顺序就是输入顺序或任意稳定排序，但不出错。
  * 简单链：A → B → C，检查 InsertOrder = [A,B,C]，DeleteOrder = [C,B,A]。
  * 分支结构：A → B, A → C, B → D，检查排序合理且所有表只出现一次。
  * 循环：A ↔ B，或 A→B→C→A，检测 loop 并返回包含所有相关表名的错误信息。

---

### Task 3.5：实现每个 SyncJob 的「依赖闭包」校验逻辑

**目标**
根据 PRD 的要求，确保每个 `SyncJob` 的 **目标表集合（TableTask 列表）是依赖闭合的**：
任何选中的目标表，如果有 FK 指向另一目标表，那么被指向的表也必须在本 job 中被选中。

**主要产物**

* 类 `JobDependencyValidator`（或类似命名），放在 `ReportSyncer.Core.Schema.Dependency`：

  * 输入：

    * 当前 `SyncJob`（含其 `TableTask` 列表）。
    * 目标 `SchemaSnapshot`。
  * 行为：

    * 先筛出本 job 参与的所有目标表。
    * 使用 FK 信息检查每个表的依赖：

      * 对于 FK 指向的目标表：

        * 如果目标表不在本 job 的 TableTask 集合中，记录一条错误：

          * `DependentTable`（当前选中表）
          * `MissingDependencyTable`（未选中，但被依赖的表）
    * 如果有任何缺失依赖，返回失败结果，错误内容必须：

      * 列出所有 `DependentTable`。
      * 列出所有需要补上的 `MissingDependencyTable`。
* 对外接口：

  * 不直接做拓扑排序，只负责「闭包检查」。
  * 可与 `IDependencyResolver` 协同使用：闭包不过就不给 plan。

**测试要点**

* 纯内存 schema：

  * 完全闭包的 job：无错误。
  * job 只选子表，不选父表：

    * 返回至少一条错误，指出子表和缺失的父表。
  * 更复杂图中只漏一部分依赖，确保错误能覆盖所有缺失项。
  * 错误信息聚合成可读结构（便于前端/CLI 展示）。

---

### Task 3.6：Schema & Dependency Facade，供 PreFlight / Orchestrator 调用

**目标**
对上层（`PreFlightValidator` / `SyncOrchestrator`）提供一个统一入口，而不是到处手动拼 ISchemaInspector / SchemaMapper / IDependencyResolver。

**主要产物**

* 新接口 `ISchemaService`（命名随你，只要清晰）：

  * 示例方法：

    ```csharp
    Task<Result<SchemaAnalysisResult>> AnalyzeJobAsync(
        SyncConfiguration config,
        SyncJob job,
        CancellationToken ct);
    ```
  * `SchemaAnalysisResult` 包含：

    * 源/目标 `SchemaSnapshot`
    * 每个 `TableTask` 的 `TableMapping`
    * 当前 job 的 `DependencyPlan`
* 实现类 `SchemaService`：

  * 内部调用：

    * `ISchemaInspector`（源 + 目标）
    * `SchemaMapper`
    * `IDependencyResolver`
    * `JobDependencyValidator`
  * 统一组合错误：

    * 连接 / schema 拉取失败
    * 映射失败
    * 依赖闭包检查失败
    * 拓扑排序失败
* 与后续章节的关系：

  * Section 4 / PreFlight 只需要调用 `ISchemaService.AnalyzeJobAsync`，不直接了解底层细节。

**测试要点**

* 使用 stub / fake 的 ISchemaInspector / SchemaMapper / IDependencyResolver / JobDependencyValidator：

  * 成功路径：所有子组件都成功，`SchemaAnalysisResult` 完整。
  * 任何一步失败：Facade 传播统一的错误结构（包含 inner 错误信息，不吞掉上下文）。

