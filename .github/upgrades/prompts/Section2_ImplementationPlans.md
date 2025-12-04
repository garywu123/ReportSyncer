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

|Test method name|Why test this|Expected result|
|---|---|---|
|`LoadAsync_WithValidMinimalConfigFile_LoadsSuccessfully`|Prove that a minimal, on-disk YAML file goes through real file IO + YAML parsing and returns a valid config.|Given `sync_valid_minimal.yaml` on disk, loader completes without exception; `SyncConfiguration` is non-null; basic counts and key values match the file.|
|`LoadAsync_WithValidFullConfigFile_LoadsAllSections`|End-to-end check that a realistic full config file loads correctly with IO + parsing + mapping.|Given `sync_valid_full.yaml`, loader completes without exception; all sections (run, safety, schema, connections, jobs, tables, filters) are populated and match the YAML contents.|
|`LoadAsync_WithConfigFileContainingUnknownKey_ThrowsConfigurationException`|Ensure unknown keys in a real file are rejected with a clear error instead of being ignored.|Given `sync_invalid_unknown_key.yaml` with extra field(s), call throws `ConfigurationException`; message mentions unknown field / invalid configuration and ideally includes the field name.|
|`LoadAsync_WithConfigFileContainingInvalidPolicy_ThrowsConfigurationException`|Validate real-file behavior when policy-like values are invalid (schema policy, safety mode, etc.).|Given `sync_invalid_schema_policy_value.yaml`, call throws `ConfigurationException`; message indicates invalid value and which setting failed.|
|`LoadAsync_WithConfigFileContainingBadYaml_ThrowsConfigurationException`|Verify corrupt YAML on disk is reported clearly to the caller.|Given `sync_invalid_syntax.yaml`, call throws `ConfigurationException`; message points to YAML parse failure and includes the file path or enough context for troubleshooting.|
|`LoadAsync_WithNonExistingFilePath_ThrowsConfigurationException`|Ensure missing files become a clean configuration error that higher layers can show to the user.|Passing a non-existent path throws `ConfigurationException`; message includes the missing path; no raw `FileNotFoundException` leaks out of the loader boundary.|

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
    
    - How hosts are expected to influence runs (CLI arguments, API payloads).
        

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

