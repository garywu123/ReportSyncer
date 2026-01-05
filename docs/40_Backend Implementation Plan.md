**Reference Documents:**

- `10_Product Requirements Document (PRD).instruction.md` (Product Behavior)
- `30_ReportSyncer Backend Arch.instruction.md` (Architecture, Classes, Interfaces)
- `20_ReportSyncer Backend Project Instruction.instruction.md` (Project General Arch)

# Section 1: DotNetToolkit – Hardening & NuGet Packaging

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

### Task 1.2 – Logging: Migrate to MEL `ILogger<T>` and host Serilog bootstrap

**Goal**  
Adopt `Microsoft.Extensions.Logging.ILogger<T>` as the canonical logging API for all production code. Hosts are responsible for creating the Serilog pipeline and registering it as the MEL provider (e.g. via `AddSerilog`). Remove the `ILogService` indirection from new code and plan migration for existing uses.

**Inputs**

- Host-level Serilog bootstrapper design (RingBuffer/File/Console sinks).
- Documentation and migration playbook (see docs/ImplementationSteps/AdditionalUpdates/20.修复 Logging.md).

**What must be implemented**
- Implement a host-side `SerilogBootstrapper` that creates a single Serilog pipeline based on `IConfiguration` and registers it with MEL (`Log.Logger` + `AddSerilog(Log.Logger, dispose:false)`).
- Ensure the Serilog pipeline includes a safe RingBuffer sink, an async File sink (JSONL), and an optional Console sink controlled by UI mode.
- Provide DI registration helpers in toolkit hosts for wiring Serilog into `IServiceCollection` (e.g., `AddSerilogAsLoggingProvider(this IServiceCollection, IConfiguration)`).
- Create a migration plan for replacing `DotNetToolkit.Logging.ILogService` usages with `ILogger<T>` across projects and tests (mechanical replace patterns, mock/test logger helpers).

**Constraints & freedom**
- Core must not reference Serilog types; it may depend on `Microsoft.Extensions.Logging` and should use `ILogger<T>`.
- Avoid introducing a custom logging abstraction in Core. Do not add a new cross-cutting `ILogService` abstraction.

**Expected behavior / tests**
- Unit tests for the `SerilogBootstrapper` that validate pipeline composition given different configurations.
- Migration tests: small integration test that boots a host, writes an `ILogger<T>` message, and verifies the message arrives in the RingBuffer snapshot.
    

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
    
    - `AddLoggingServices` / `AddSerilogAsLoggingProvider` helpers to register Serilog as the `Microsoft.Extensions.Logging` provider and to configure host-level sinks.
        

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

# Section 2 (Revised): Configuration & Error Model – Implementation Plan

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

**Note on error classification**: 
- **File I/O errors** (missing file, access denied) should propagate as `FileNotFoundException` or `IOException` – these are infrastructure problems, not configuration content problems.
- **Configuration content errors** (invalid YAML syntax, missing required fields, invalid enum values) should throw `ConfigurationException`.

| Test method name                                                               | Why test this                                                                                                | Expected result                                                                                                                                                                              |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LoadAsync_WithValidMinimalConfigFile_LoadsSuccessfully`                       | Prove that a minimal, on-disk YAML file goes through real file IO + YAML parsing and returns a valid config. | Given `sync_valid_minimal.yaml` on disk, loader completes without exception; `SyncConfiguration` is non-null; basic counts and key values match the file.                                    |
| `LoadAsync_WithValidFullConfigFile_LoadsAllSections`                           | End-to-end check that a realistic full config file loads correctly with IO + parsing + mapping.              | Given `sync_valid_full.yaml`, loader completes without exception; all sections (run, safety, schema, connections, jobs, tables, filters) are populated and match the YAML contents.          |
| `LoadAsync_WithConfigFileContainingUnknownTopLevelKey_ThrowsConfigurationException` | Ensure unknown **top-level** keys in a real file are rejected with a clear error.                      | Given `sync_invalid_unknown_key.yaml` with extra top-level field(s), call throws `ConfigurationException`; message mentions unknown field and ideally includes the field name.              |
| `LoadAsync_WithConfigFileContainingInvalidPolicy_ThrowsConfigurationException` | Validate real-file behavior when policy-like values are invalid (schema policy, safety mode, etc.).          | Given `sync_invalid_schema_policy_value.yaml`, call throws `ConfigurationException`; message indicates invalid value and which setting failed.                                               |
| `LoadAsync_WithConfigFileContainingBadYaml_ThrowsConfigurationException`       | Verify corrupt YAML on disk is reported clearly to the caller.                                               | Given `sync_invalid_syntax.yaml`, call throws `ConfigurationException`; message points to YAML parse failure and includes the file path or enough context for troubleshooting.               |
| `LoadAsync_WithNonExistingFilePath_ThrowsFileNotFoundException`                | Ensure missing files are reported as I/O errors, not configuration errors.                                   | Passing a non-existent path throws `FileNotFoundException` (or `IOException`); this is an infrastructure problem, distinct from configuration content validation.                            |

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


# Section 3: Schema & Dependency Subsystem (`ReportSyncer.Core.Schema`)

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
  * `ColumnMapping`：`SourceColumn?`, `TargetColumn`, `MappingKind`（`OneToOne` / `Constant` / `ContextColumn` / `Ignored`）
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
      * `Source.Type == Application && Target.Type == Reporting` 时，自动在目标侧补上 `Context Column`：
        * 从 job parameter 中取值，生成 `ColumnMapping`（`MappingKind = ContextColumn`）。
        * 如果目标已经有同名列且来源有值，触发“歧义保护”错误。
    * 做基础类型兼容性检查（按类型家族，`nvarchar` ↔ `varchar` / `int` ↔ `bigint` 等）。可以尝试将这个类型检查作为工具存放到 `DotNetToolkit.Database` 中
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

# Section 4 — PreFlight & Sync Orchestration（任务列表）


> 先把话说清楚：你现在的 Core 代码里 **确实没有** `ReportSyncer.Core.Sync/*`，也 **没有** `ISchemaService / SchemaService`，更没有你吐槽的 `IDependencyResolver`（表依赖拓扑排序那套）。
> 
> 你已经完成/具备的（Section 3 可复用）：
> - `ReportSyncer.Core.Configuration`：`IConfigurationProvider/ConfigurationProvider`（含 `LoadAndValidateAsync(path, overrides, ct)`）、YAML loader、validator、`ConfigurationException`。
> - `ReportSyncer.Core.Schema`：`ISchemaInspector` + `SqlServerSchemaInspector`（至少支持 `SchemaInspectionLevel.Full`）、`SchemaSnapshot/TableSchema/ColumnSchema/ForeignKeySchema` 等模型。
> - `ReportSyncer.Core.Schema.Mapping`：`ISchemaMapper` + `SchemaMapper`（Result 风格）。
> - `ReportSyncer.Core.Exceptions`：`SchemaMismatchException`、`SyncExecutionException`。

> 你**没有**的（所以本 Section 必须补齐，否则 Section 4 根本没法衔接）：
> - `IDependencyResolver` / `ExecutionPlan`（PRD 依赖排序 + 缺失 parent 表检测要求）。
> - `ISchemaService` / `SchemaService`（原 Implementation Plan 里写在 Task 3.6，但你没实现）。
> - `PreFlightValidator` / `SyncOrchestrator`（Sync 领域入口）。
---

## 4.1 本 Section 的目标（可验证）

- **Goal A（PreFlight Gate）**：任何 schema/mapping/dependency 失败必须阻断后续执行（先只做到“阻断”，真正执行留到 Section 5）。
- **Goal B（依赖排序与缺失父表报错）**：对选中的 Target tables 建图、检测 missing parents、拓扑排序输出可执行顺序。
- **Goal C（Backend 入口成型）**：提供 `ISyncOrchestrator.RunJobAsync(...)`，能从 configPath 装载配置、resolve job、跑完 preflight 并返回结构化结果（DryRun 模式下返回 “不会执行任何 DML” 的结果）。
**Non-goals（这轮不做）**
- 不做真实 Delete/Insert（`ITableRunner` / `SqlDataWriter` 属于 Section 5）。
- 不做 permissions/safety/work-estimation（文档里有，但代码里你也没实现，别硬塞进来）。

---

## 4.2 Task 列表（更新版，按“真实缺口”重新排序）

### Task 4.0 — 实现 Dependency Planning（IDependencyResolver + ExecutionPlan）

**新增路径（建议）**
- `ReportSyncer.Core/Schema/Dependency/IDependencyResolver.cs`
- `ReportSyncer.Core/Schema/Dependency/DependencyResolver.cs`
- `ReportSyncer.Core/Schema/Dependency/ExecutionPlan.cs`
- `ReportSyncer.Core/Schema/Dependency/DependencyError.cs`（或 `DependencyErrorCode`）
    
**接口契约（英文）**

```csharp
namespace ReportSyncer.Core.Schema.Dependency;

public interface IDependencyResolver
{
    Result<ExecutionPlan> BuildExecutionPlan(
        SchemaSnapshot targetSnapshot,
        IReadOnlyList<TableIdentifier> selectedTargetTables);
}

public sealed record ExecutionPlan(
    IReadOnlyList<TableIdentifier> InsertOrder,
    IReadOnlyList<TableIdentifier> DeleteOrder);
```

**规则（写死）**

- Rule 1：只使用 **Target FK 图** 建依赖（Source FK 不参与排序）。
- Rule 2：如果 selected 表 A 有 FK 指向表 B，但 B 不在 selected 集合里 → `Result.Fail`（错误里必须带 A、B）。
- Rule 3：如果图中存在 cycle → `Result.Fail`（错误里列出 cycle path，最少给出参与节点）。
- Rule 4：输出 `InsertOrder = parents -> children`，`DeleteOrder = reverse(InsertOrder)`（先简单正确）。
    
**Definition of Done（测试）**
- Unit:
    - `DependencyResolver_MissingParentTable_FailsWithClearError`
    - `DependencyResolver_WithTwoTablesOneFk_ReturnsParentBeforeChild`
    - `DependencyResolver_WithCycle_Fails`
---

### Task 4.1 — 补齐 Schema Facade：ISchemaService / SchemaService（从原 Task 3.6 搬过来）

**新增路径（建议）**

- `ReportSyncer.Core/Schema/Services/ISchemaService.cs`
- `ReportSyncer.Core/Schema/Services/SchemaService.cs`
- `ReportSyncer.Core/Schema/Services/SchemaAnalysisResult.cs`
    
**接口契约（英文）**

```csharp
namespace ReportSyncer.Core.Schema.Services;

public interface ISchemaService
{
    Task<Result<SchemaAnalysisResult>> AnalyzeJobAsync(
        SyncConfiguration effectiveConfig,
        SyncJobConfig job,
        CancellationToken ct);
}

public sealed record SchemaAnalysisResult(
    SchemaSnapshot SourceSnapshot,
    SchemaSnapshot TargetSnapshot,
    SchemaMappingResult Mapping,
    ExecutionPlan ExecutionPlan);
```

**实现要点（不允许乱）**

1. 解析 job 的 selected tables（只包含 enabled 的 table tasks，且以 **Target 表名**作为依赖图节点）。
2. 调用 `ISchemaInspector`：
    - Source：用“最低成本、只保证表存在 + 列信息够 mapping 的 level”（你的 enum 名字以 repo 为准）。
    - Target：`SchemaInspectionLevel.Full`。
        
3. 调用 `ISchemaMapper` 生成 mapping。
4. 调用 `IDependencyResolver.BuildExecutionPlan(targetSnapshot, selectedTargets)`。
5. 任一步失败：返回 `Result.Fail`，**不要在这里 throw**（throw 留给 PreFlight 做统一翻译）。
    
**Definition of Done（测试）**
- Unit（用 fake/stub，不碰真实 DB）：
    - `SchemaService_WhenInspectorFails_ReturnsFail`
    - `SchemaService_WhenMapperFails_ReturnsFailWithMappingErrors`
    - `SchemaService_WhenDependencyFails_ReturnsFailWithDependencyErrors`
    - `SchemaService_WhenAllOk_ReturnsCompleteSchemaAnalysisResult`
        

---

### Task 4.2 — 定义 Sync 领域 Contracts（DTO + 状态枚举 + 接口）

**新增路径（建议）**

- `ReportSyncer.Core/Sync/ISyncOrchestrator.cs`
- `ReportSyncer.Core/Sync/IPreFlightValidator.cs`
- `ReportSyncer.Core/Sync/Contracts/JobStatus.cs`
- `ReportSyncer.Core/Sync/Contracts/TableStatus.cs`
- `ReportSyncer.Core/Sync/Contracts/JobResult.cs`
- `ReportSyncer.Core/Sync/Contracts/TableResult.cs`
- `ReportSyncer.Core/Sync/Contracts/PreFlightResult.cs`
    
**接口契约（英文）**

```csharp
namespace ReportSyncer.Core.Sync;

public interface ISyncOrchestrator
{
    Task<JobResult> RunJobAsync(
        string configPath,
        string jobName,
        RuntimeOverrides? overrides,
        CancellationToken ct);
}

public interface IPreFlightValidator
{
    Task<PreFlightResult> ValidateAsync(
        SyncConfiguration effectiveConfig,
        SyncJobConfig job,
        CancellationToken ct);
}
```

---

### Task 4.3 — 实现 PreFlightValidator（只做检查 + 产出计划）

**新增路径**
- `ReportSyncer.Core/Sync/PreFlightValidator.cs`

**依赖（必须）**
- `ISchemaService`（Task 4.1）

**规则（写死）**
- Rule 1：`AnalyzeJobAsync` 返回 Fail → 抛 `SchemaMismatchException`，message 必须包含 job name + 至少一个失败原因（mapping 或 dependency）。
- Rule 2：`RunConfig.dryRun` 只作为 flag 透传，不在这里做执行决策（决策在 Orchestrator）。
    
**Definition of Done（测试）**
- Unit:
    - `PreFlightValidator_WhenSchemaServiceReturnsFailure_ThrowsSchemaMismatchExceptionWithDetails`
    - `PreFlightValidator_CallsSchemaServiceOnce_WithJobSelectedTables`
    - `PreFlightValidator_DryRunFlag_PropagatesToPreFlightResult`
- Integration（建议用 LocalDB，2 表 1 FK）：
    - `PreFlightValidator_WithLocalDbSchema_ReturnsExecutionPlan`
    - `PreFlightValidator_SourceMissingTable_ThrowsSchemaMismatchException`
        

---

### Task 4.4 — 实现 SyncOrchestrator（Load → Resolve Job → PreFlight → 返回结果）

**新增路径**
- `ReportSyncer.Core/Sync/SyncOrchestrator.cs`
**依赖（必须）**
- `IConfigurationProvider`（已存在）
- `IPreFlightValidator`（Task 4.3）
    
**规则（写死）**
- Rule 1：必须先 `LoadAndValidateAsync(configPath, overrides, ct)`，再 resolve job，再 preflight。
- Rule 2：PreFlight 抛任何异常 → Orchestrator 不得吞，直接冒泡。
- Rule 3：如果是 dry-run：返回 `JobResult`，每张表标记 `Skipped_DryRun`（或等价状态）。
- Rule 4：如果不是 dry-run：明确抛 `SyncExecutionException("Execution engine not implemented. Implement Section 5.")`。
    
**Definition of Done（测试）**
- Unit:
    - `SyncOrchestrator_LoadsConfig_ResolvesJob_ThenCallsPreFlight`
    - `SyncOrchestrator_WhenPreFlightThrows_DoesNotProceed`
    - `SyncOrchestrator_WhenDryRun_ReturnsJobResultWithSkippedTables`
        

---

### Task 4.5 — 建立可测试缝（Fakes / Test Helpers）

**目的**：让 Unit Test 不需要真实 DB、不需要真实 YAML（你 YAML 单测已经做过了，别再浪费生命）。

**建议产物**

- `ReportSyncer.Core.Tests/Helpers/FakeSchemaService.cs`
    
- `ReportSyncer.Core.Tests/Helpers/FakeConfigurationProvider.cs`
    
- `ReportSyncer.Core.Tests/Helpers/TestJobFactory.cs`
    

---

## 4.3 为什么这版能衔接（而不是“驴唇不对马嘴”）

- 之前的 Section 4 假设你已经完成 `ISchemaService` 和 `IDependencyResolver`，但你没做，所以当然对不上。
    
- 这版 Section 4 把**真实缺口**前置成 Task 4.0/4.1，然后才开始 Sync 领域（4.2+）。
    
- 测试面只聚焦：dependency planner、schema facade、preflight gate、orchestrator 生命周期。YAML 单测不重复。




---
# Section 5 — Runtime Execution Pipeline（从 Plan 到真实删+插，但先上“安全门”）

## 主要实现目标
### 核心目标
- 将 **Preflight 产物（ExecutionPlan/DAG + mapping + schema snapshot）** 转化为**可重复、可预测**的执行。
- 支持 **DryRun → RealRun** 的统一 pipeline：DryRun 用于估算/验证/报告，不改数据；RealRun 才做 DML。
- 明确并落地 **幂等语义**（建议 MVP：Overwrite）。
### 最小可观测性（为了让 Section 5 能被测试与排障）

- 在实现 RealRun 之前，必须具备**最小级别**的可观测性，否则后续 Integration Test 失败你只能靠猜：
    - 结构化日志最小相关字段：`JobId` / `Table` / `Phase`（delete/insert/dryrun/preflight-gate）
    - 进度事件最小模型：Start/End/Skip/Fail（未来 GUI/WebApi 只是消费者）
    - 在 pipeline 中必须保证 Observability 初始化发生在门禁与执行之前（见文末统一 pipeline）
- 这部分不要求你一次到位做“完整 Run Report / Retry / 错误聚合”，那些归到 Section 6/7；这里强调的是：**没有最小 telemetry，Section 5 根本不可维护**。
    
### 必须做对的决策点（重要算法/语义）
- **执行顺序**：只能来自 Preflight DAG/ExecutionPlan；执行期禁止“临时重排”。
- **幂等策略（必须写死）**：
    - MVP 推荐：`Overwrite`（在过滤范围内先删后插，重跑两次结果一致）。
    - 明确 Non-goal：Upsert/Merge 以后再做。
- **Identity Insert 自动化规则（不要自欺欺人）**：
    - 只有当 insert 列表**显式包含 identity 列**且来源是“外部值”时才需要开启 `IDENTITY_INSERT`。
    - `IDENTITY_INSERT` 是 session 级别且同 session 只允许一张表 ON，禁止在同一连接上并行多表写入。
- **事务/批处理边界（选一个，别两套并存）**：
    - 默认建议：每表一个事务（可控、易回滚、锁时间短）。
    - 明确 batch size、超时、失败回滚的边界。
        
### Guardrails（把 Safety/Permission 直接并入执行入口，不给“绕过”的机会）
- RealRun 的入口必须被“门禁”保护：
    - **Permission Profiling**（DryRun vs RealRun 要分级）
    - **Safety Rules**（集中式规则，不允许散落在执行器里）
    - （可选但常用）强制先 DryRun 再 RealRun
- 失败必须是 **fail-fast**，且错误信息可行动（哪个表、哪个动作、缺什么权限/违反哪条规则）。
    

### 关键约束（硬约束）
- 任何 FK 顺序/不可执行性必须在 Preflight 阶段暴露，执行期不“碰运气”。
- 所有 SQL 必须参数化（禁止拼接值）。
- MVP 阶段不做：并行表执行、多事务策略并存、Upsert/Merge。
    
### 验收标准（可验证）
- DryRun：执行结束后数据完全不变；能输出估算信息。
- RealRun：
    - 2 表 FK 场景：父表先于子表写入。
    - Identity 场景：仅在需要时开启/关闭 identity_insert，且成对。
    - 可重复运行：同一输入跑两次，结果符合 Overwrite 语义。

## 主要 Tasks

### Input Context（你要求的 4 件套）

1. **Source of Truth（PRD / Behavior）**：ReportSyncer PRD（安全规则、DryRun、Identity Insert、Chunked Delete、依赖顺序等）
2. **Architecture Standards（Layout / Contracts）**：ReportSyncer Backend Architecture + Detail Arch（分层、接口归属、异常模型、Sync/Security/Observability 的责任边界）
3. **Current State（你已完成 Section 1-4）**：已有 Config / Schema / Mapping / Dependency / PreFlight 基础能力，SyncOrchestrator 真实执行尚未落地（Section 4 的终点通常是“dry-run 可以，real-run 先别碰”）。
4. **Target Section**：**Section 5 — Runtime Execution Pipeline**
    
---
### Task 5.1 — 定义执行期核心数据结构（Execution Contracts）

**Structural Scope**
- Namespace：`ReportSyncer.Core.Sync`（或 `ReportSyncer.Core.Sync.Contracts`）
- DTO/Model（建议）：
    - `TableExecutionContext`（绑定：`JobId`、`TableTaskConfig`、`SchemaMapping`/`TableMapping`、`Resolved Connections`、`DryRun` flag、`Parameters`）
    - `DeleteCommandContext` / `InsertCommandContext`（给 IDataWriter 用）
    - `SyncPhase`（`Preflight`, `Estimate`, `Delete`, `Insert`）
    - `TableResult` / `JobResult`（若已有则扩字段，不重复造轮子）

**Interface Contract（关键方法/返回）**
- 这些结构必须能支撑：`ITableRunner.RunAsync(TableExecutionContext, CancellationToken) -> Task<TableResult>`
- 以及：`IDataWriter.DeleteAsync(DeleteCommandContext, ct) -> Task<int>`、`IDataWriter.InsertAsync(InsertCommandContext, ct) -> Task<int>`

**Logic & Invariants**
- Must：所有执行所需信息必须来自 Preflight 产物，不允许执行期“临时猜测 schema/重排顺序”。
- Must-Not：执行期 DTO 不允许持有 Host/UI 类型（Console/WebApi/WPF 的引用一律禁止）。
- Error Handling：DTO 本身不 throw；后续执行器统一抛 `SyncExecutionException` / `SafetyViolationException`（按责任分层）。

**Definition of Done（Tests）**
- Unit：构造最小 `TableExecutionContext` 不需要 DB 即可表达 Delete/Insert 所需字段。
- Unit：序列化（如果你要用于 history/IPC）必须稳定（字段可选，但别今天叫 A 明天叫 B）。
    
---

### Task 5.2 — 实现 SQL Query Builder（参数化 Delete/Select/Insert 的生成器）

**Structural Scope**
- Namespace：`ReportSyncer.Core.Sync.Sql`（或 `ReportSyncer.Core.Sync.Internal`）
- Types：`SqlServerQueryBuilder`（或 `SqlCommandFactory`）
    
**Interface Contract**
- 典型方法（表达意图即可）：
    - `BuildDelete(DeleteCommandContext) -> DbCommandSpec`
    - `BuildSelectSource(InsertCommandContext) -> DbCommandSpec`
    - `BuildInsertTarget(InsertCommandContext) -> DbCommandSpec`（如果用 TVP/BulkCopy，这里返回策略描述而不是一条 SQL）
    - `BuildCountEstimate(TableExecutionContext) -> DbCommandSpec`
        
**Logic & Invariants（直接绑定 PRD 规则）**
- Must：**所有 SQL 参数化**（禁止拼接值）。
- Must：Filter（CustomerId / DateRange）必须映射为 WHERE 子句，并严格 AND 组合（PRD 已明确“可同时存在”）。
- Must：Context Injection（Application → Reporting）只影响 Insert 列清单与参数，不允许在 Source 上做任何写入。
- Must-Not：不要把 Safety 规则写进 QueryBuilder（比如 allowAllDelete 检查），QueryBuilder 只负责“怎么写 SQL”。
    

**Error Handling Requirements**
- QueryBuilder 遇到“无法表达的命令”（比如没有任何 filter 但又被要求 scoped delete）应抛 `ConfigurationException` 或返回 fail（看你现在的错误模型一致性），但**不要**默默生成 `DELETE FROM table` 这种灾难。
    

**Definition of Done（Tests）**
- Unit：
    - “给定 FilterConfig，生成的 DbCommandSpec 必须包含参数，不包含拼接值”
    - “Context Column mapping 会导致 Insert 列多一列 + 多一个参数”
- Integration（可选）：对 LocalDB 执行 `SELECT COUNT(*) WHERE ...` 确认语法正确。
    

---

### Task 5.3 — 实现 Work Estimator（DryRun 估算 + Safety 大删除阈值输入）

**Structural Scope**
- Namespace：`ReportSyncer.Core.Observability`（或临时放 `Sync`，但建议归 Observability，因为它是纯“估算/可观测”能力）
- Types：`WorkEstimator`, `WorkEstimate`, `EstimatedDeleteStats`
    
**Interface Contract**
- `EstimateAsync(TableExecutionContext, ct) -> Task<WorkEstimate>`
- `WorkEstimate` 至少包含：`EstimatedRowsToDelete`, `EstimatedRowsToInsert`, `EstimatedDeletePct?`, `Warnings`
    
**Logic & Invariants**
- Must：DryRun 必须调用估算，但 **不得做任何 DML**。
- Must：估算结果要能支撑 Safety 的 `confirmLargeDeletePct` 判断。
- Must-Not：估算失败时不要“当作 0 行然后继续”，这会让 Safety 形同虚设。

**Error Handling**
- DB 失败：抛 `SyncExecutionException`（属于运行时故障，不是配置问题）。

**Definition of Done（Tests）**
- Unit：QueryBuilder 被正确调用（WHERE 条件正确传递）。
- Integration（LocalDB）：
    - 有过滤条件时估算数量正确
    - 无过滤条件时能返回全表 count（但后续 Safety 可能会拦）
        

---

### Task 5.4 — 实现 Permission Profiler（DryRun/RealRun 权限分级门禁）

**Structural Scope**
- Namespace：`ReportSyncer.Core.Security`
- Types：`IPermissionProfiler`, `SqlServerPermissionProfiler`, `PermissionsProfile`

**Interface Contract**
- `ProbeTablePermissionsAsync(TableExecutionContext, ct) -> Task<PermissionsProfile>`
- `PermissionsProfile` 至少：`CanDelete`, `CanInsert`, `CanSetIdentityInsert`
    
**Logic & Invariants**
- Must：权限探测必须是 **no-op**（比如事务回滚、DELETE TOP(0)、SET IDENTITY_INSERT 探测后恢复）。
- Must：DryRun 允许只探测读/Count 权限；RealRun 必须探测 Delete/Insert/IdentityInsert（按 table config）。
- Must-Not：不要用“直接试着 delete 一行”这种愚蠢方式做探测（你会污染目标数据，然后还自称安全）。
    
**Error Handling**
- 无权限：不抛异常，返回 flags（false）。
- 其它 SQL 故障：抛 `SyncExecutionException`。
    
**Definition of Done（Tests）**
- Unit：无权限场景 -> flags false。
- Integration（LocalDB）：
    - 正常用户 -> flags true
    - （可选）用受限用户验证 Delete/Insert 探测。
        

---

### Task 5.5 — 实现 Safety Validator（集中式 Guardrails，禁止散落）

**Structural Scope**
- Namespace：`ReportSyncer.Core.Security`
- Types：`ISafetyValidator`, `SafetyValidator`, `IEnvironmentClassifier`（若你还没做环境识别）
    
**Interface Contract**
- `ValidateJob(jobCtx) -> void`
- `ValidateTableTask(jobCtx, tableCtx, estimate) -> void`（estimate 来自 Task 5.3）
    
**Logic & Invariants（PRD 强绑定）**
- Must：禁止 Prod→Prod（forbidProdToProd）。
- Must：禁止 self-sync（source/target 同库）。
- Must：`preSyncTargetAction=true` 且无 filter 时，必须拦截，除非 `allowAllDelete=true`。
- Must：大删除阈值（confirmLargeDeletePct）触发时必须“需要显式确认”。
    

**关键问题（你需要面对，不然需求不完整）**  
PRD 说“大删除要暂停等待用户确认”，但 Core 没有“交互式确认通道”的定义。你只有两条路：
1. **保守模式（推荐）**：直接阻断并返回 “ConfirmationRequired” 结果，让 UI/Host 再次发起（带一个 runtime override acknowledgement）。
2. 真暂停：Core 进入等待状态，靠 IPC/UI 继续，这会把你拖进并发/状态机地狱，不适合 MVP。
    

把这个确认机制当成 **Section 5 的必要 Task**，否则你在安全要求上是假的。
**Error Handling**
- 违反 Safety：抛 `SafetyViolationException`（信息必须可行动：Job/Table/Rule）。
**Definition of Done（Tests）**
- Unit：
    - prod→prod 阻断
    - allowAllDelete=false 且无 filter 阻断
    - 大删除 pct 超阈值 -> 产生 ConfirmationRequired（或 SafetyViolationException，取决于你选的机制，但必须可测）
        

---

### Task 5.6 — 扩展 PreFlight：纳入 Permission + Safety + Work Estimate（真正的“门禁”）

**Structural Scope**
- Namespace：`ReportSyncer.Core.Sync`
- Types：`PreFlightValidator`（扩展现有）
    
**Interface Contract**
- `ValidateAsync(effectiveConfig, job, ct) -> Task<PreFlightResult>`（你已有的话就扩字段）
- `PreFlightResult` 新增：
    - `PermissionsProfile`（按表/按连接）
    - `WorkEstimate` / `EstimatedDeleteStats`（按表）
    - `SafetyDecision`（OK / ConfirmationRequired / Blocked）
        

**Logic & Invariants**
- Must：任何失败必须发生在 DML 前。
- Must：Preflight 必须产出“执行所需的全部输入”，执行期不再二次探测（避免执行期才发现权限/安全问题）。
- Must-Not：不要把写入 SQL 放进 Preflight（Preflight 只产出计划与门禁结果）。
    

**Error Handling**
- Schema/Dependency：`SchemaMismatchException`（你已有）
- Safety：`SafetyViolationException` 或 “ConfirmationRequired” 可返回型结果（看你选的机制）
- DB 故障：`SyncExecutionException`
    

**Definition of Done（Tests）**
- Unit：Preflight 会调用：SchemaService -> WorkEstimator -> PermissionProfiler -> SafetyValidator（顺序固定）。
- Integration：在 LocalDB 上跑完整 preflight，返回完整 PreFlightResult。
    

---

### Task 5.7 — IdentityInsertManager（严格成对 ON/OFF，失败也得 OFF）
**Structural Scope**
- Namespace：`ReportSyncer.Core.Sync`
- Types：`IdentityInsertManager`
    

**Interface Contract**
- `BeginAsync(targetContext, tableName, ct) -> Task<IAsyncDisposable>`（或等价方式）
- 目标：让 TableRunner 用 `await using` 级别保证 OFF 执行。
    

**Logic & Invariants（PRD 强绑定）**
- Must：只在需要时开启（insert 列清单包含 identity 且来源为外部值，并且 table config 允许）。
- Must：同一连接同一时刻只能一张表 IDENTITY_INSERT ON（禁止并行写）。
- Must：失败/取消也要 OFF。
    

**Error Handling**
- ON/OFF 执行失败：抛 `SyncExecutionException`（并包含 table/phase）。
    

**Definition of Done（Tests）**
- Unit：无论 Insert 成功/失败/抛异常，都调用 OFF。
- Integration：LocalDB identity 表，开启后插入带 identity 值成功。
    

---

### Task 5.8 — SqlDataWriter（Chunked Delete + Batched Insert，真实 DML 引擎）
**Structural Scope**
- Namespace：`ReportSyncer.Core.Sync`
- Types：`IDataWriter`, `SqlDataWriter`
    

**Interface Contract**
- `DeleteAsync(DeleteCommandContext, ct) -> Task<int>`（返回删除行数）
- `InsertAsync(InsertCommandContext, ct) -> Task<int>`（返回插入行数）
    

**Logic & Invariants（PRD/Plan 强绑定）**
- Must：Delete 必须 chunked（避免 log 爆炸）。
- Must：Insert 必须 batched（batch size 来自 RunConfig），且要可取消。
- Must：Source 永远只读，不允许任何 DML。
- Must-Not：不要实现 Upsert/Merge（Section 5 明确 non-goal）。
    

**错误与异常要求**
- SQL 超时/约束冲突/网络抖动：抛 `SyncExecutionException`，消息必须包含：table、phase、关键参数（但不要泄露 connection string 全量）。
    

**Definition of Done（Tests）**
- Unit：
    - DeleteAsync 以 chunk 循环直到 0 行
    - InsertAsync 按 batch 调用底层执行
- Integration（LocalDB）：
    - 过滤 delete + insert 后数据符合预期
    - 大表（模拟）不会一次性 delete 全表（验证 chunk 行为）
        

---

### Task 5.9 — TableRunner（单表 Overwrite 语义：Delete → Insert）

**Structural Scope**
- Namespace：`ReportSyncer.Core.Sync`
- Types：`ITableRunner`, `TableRunner`
    

**Interface Contract**
- `RunAsync(TableExecutionContext, ct) -> Task<TableResult>`
    
**Logic & Invariants**
- Must：Overwrite 语义落地：在 filter 范围内先删后插，重跑两次结果一致。
- Must：尊重 `preSyncTargetAction`（为 false 时跳过 delete）。
- Must：IdentityInsert scope 由 TableRunner 管理（不要让 Orchestrator 管）。
- DryRun：只做 Estimate + 产出 TableResult（不得触发 DataWriter）。
    

**Error Handling**
- DataWriter 抛错：TableRunner 包装/补充上下文后抛 `SyncExecutionException`（必须带 jobId/table/phase）。
    
**Definition of Done（Tests）**
- Unit：用 fake `IDataWriter` 验证调用顺序 delete→insert、以及 dryrun 不调用 writer。
- Integration：LocalDB 两次运行结果一致。
    

---

### Task 5.10 — SyncOrchestrator RealRun 落地（按 ExecutionPlan 执行，禁止“临时重排”）

**Structural Scope**
- Namespace：`ReportSyncer.Core.Sync`
- Types：`SyncOrchestrator`（扩展现有）
    
**Interface Contract**
- 仍然是：`RunJobAsync(configPath, jobName, overrides, ct) -> Task<JobResult>`（你现有签名保持一致即可）

**Logic & Invariants**
- Must：执行顺序只能来自 `ExecutionPlan`：
    - Delete：children→parents
    - Insert：parents→children
        
- Must：禁止并行表执行（Section 5 non-goal，且 identity_insert 也不允许并行）。
- Must：如果 Preflight 返回 ConfirmationRequired，Orchestrator 必须在任何 DML 前停止，并返回“被阻断”的 JobResult（或抛 SafetyViolationException，取决于你选的确认机制）。
- Must-Not：Orchestrator 不得直接写 SQL，不得绕过 TableRunner/IDataWriter。
    

**Error Handling**
- 取消：将 `OperationCanceledException` 映射为 `UserCancelledException`（或你现有约定）。
- 其它：原样抛 domain exception，让 host 做统一处理。
    

**Definition of Done（Tests）**
- Unit：验证 Delete phase 使用 DeleteOrder，Insert phase 使用 InsertOrder。
- Integration：LocalDB 有 FK 的 2 表场景，父表先插入，子表后插入。
    

---

### Task 5.11 — “最小可观测性”落地（只做 Section 5 必需的那点，不要贪）

**Structural Scope**
- Namespace：`ReportSyncer.Core.Observability`
- Types：
    - `IJobProgressReporter`（Start/End/Skip/Fail 事件）
    - `JobProgressEvent` / `TableProgressEvent`（最小 payload：JobId/Table/Phase/Rows/Elapsed）
        
**Interface Contract**
- `Report(JobProgressEvent)` / `Report(TableProgressEvent)`（同步或异步都行，但要稳定）
    
**Logic & Invariants**
- Must：DryRun/RealRun 都要发事件，不然你后续测试与排障完全靠玄学。
- Must-Not：不要在 Section 5 做 History 持久化/完整 report 文件（那是 Section 6）。

**Definition of Done（Tests）**
- Unit：跑一个 fake job，事件序列符合：JobStart -> (TableStart/TableEnd)* -> JobEnd。
- Unit：错误时必须有 Fail 事件，并带 phase/table。
    

---

### Task 5.12 — 端到端 Integration Test 组装（把 Section 5 的“验收标准”变成自动化）

**Structural Scope**
- Project：`ReportSyncer.Core.Tests`（Integration）
- Fixture：LocalDB schema + seed data（两表 FK、identity 表、历史表）

**Definition of Done（必须覆盖的集成用例）**

- DryRun：执行后数据完全不变，但能产出估算与计划。
- RealRun Overwrite：同一输入跑两次，结果一致。
- FK 顺序：父表先插入，子表后插入。
- Identity Insert：仅在需要时开启，且 ON/OFF 成对。
- Safety：无 filter 且 allowAllDelete=false 时必须阻断。
- Large delete：触发阈值时进入 ConfirmationRequired 或阻断（按你选的机制），且没有任何 DML。

---

- **大删除确认**：PRD 要“暂停等确认”，但你必须给 Core 一个可测试的确认输入（runtime override / 二次调用）。否则你只能“直接失败”，那不叫确认。
- **Safety 不能散落**：allowAllDelete / prod→prod / self-sync 这种必须集中，否则以后你会修出 3 套规则，最后谁都不知道哪套生效。
- **Source 永远只读**：你只要在 Source 上误执行一次 DML，这工具就从“同步工具”变成“职业生涯终结者”。
以上就是 Section 5 的任务清单。你把这份丢给下一个 Developer，他们就能据此写 Step-by-step playbook，而且不会把 Scope 膨胀到 Section 6/7 去。


### Integration Tests 场景
需要创建两个数据库，一个数据库作为 Src，一个作为 Target，Target 中对应 Mapping 的列，可以添加一些诸如额外的 CustomerId 列等情况。
- DryRun 模式下
	- 确保没有执行操作
	- 确保返回一系列需要插入的 Table，并且按照 FK 的顺序进行排序，如果有多个 Table 共享一个 FK，其 Order 顺序也不能变（因此你需要创建 SQL 数据库需要更复杂一些）
- RealRun 模式下
	- 常规插入，带有 FK 情况
		- 多个子表共享一个父表，推荐测试的数据库中至少有4个子 Table，外加2个父Table，按照 FK 顺序进行排序。
		- 两个表 Mapping，同时 Target Table 中有一个 CustomerId 列，需要 YAML 额外说明，插入成功。
			- YAML 没有额外配置，插入失败。
	- 常规插入，忽略 FK 情况，排序则按照 YAML 配置文件中的顺序安排
	- 常规插入，ID Insert 场景
		- 目标 Table 的确带有 ID，并且有 Mapping，成功
		- 目标 Table 有 ID 列，但没有 Mapping，失败
		- 
	- 

---

# Section 6 — Observability & Run Artifacts（先让人类知道它在干嘛）

- 核心目标
	- 在不依赖 host（Console/WebApi/GUI）的前提下，提供**可消费的运行信息**：日志、进度事件、结果报告。
- 必须做对的决策点
	- **结构化日志**：必须具备 Job/Table/Phase 的 correlation（不然无法排障）。
	- **进度事件模型**：Start/End/Skip/Fail + 最小 payload（table、rows、elapsed、error）。
	- **Run Report**：机器可读（JSON/目录输出皆可），包含：plan 摘要、每表结果、耗时、错误列表。
	- **取消**：job 级 cancellation（后续 GUI/WebApi 必需）。
- 关键约束
	- Observability 必须是 library 级能力，host 只是 sink/consumer。
	- 错误聚合必须可行动（不要只有 exception dump）。
- 验收标准
	- 跑一次 job：
	    - 日志可定位到“哪个 job / 哪张表 / 哪个阶段”。
	    - 事件序列符合预期（开始→结束/开始→失败）。
	    - report 文件落地且字段完整。

---

### Task 6.1 — 观测模型“定版”（Contracts Stabilization）

**要做什么**
- 固化 Core 可用的 Observability contracts（DTO + 接口），避免 host 绑架领域模型。
- 统一 correlation 字段：至少包含 `JobId`, `TableName`, `Phase`（以及时间戳/级别/错误摘要等）。

**关键点**
- 事件模型必须覆盖：Start/End/Skip/Fail（job 级 + table 级）。:contentReference[oaicite:3]{index=3}
- 保持 sink 无关：Core 只调用抽象接口，不知道 Console/IPC/文件怎么写。 :contentReference[oaicite:4]{index=4}

**交付物**
- `ReportSyncer.Core.Observability` 下的接口与 DTO（定版，不再随便改字段）。
- 一个默认的 `Null`/`Noop` reporter（确保 Core 永远能调用，不用到处判空）。

---

### Task 6.2 — 结构化日志落地（Structured Logging with Correlation）

**要做什么**
- 建立结构化日志能力，保证每条关键日志都能关联到 Job/Table/Phase。:contentReference[oaicite:5]{index=5}

**关键点**
- 日志不是“给人看热闹”的，必须能用于排障与追溯，所以 correlation 是硬要求。
- 日志事件应包含“可行动信息”：比如失败发生的 table、phase、错误分类（domain exception 类型）与简短原因。:contentReference[oaicite:6]{index=6}

**交付物**
- Core 层可调用的日志抽象（或复用你既有 logging 设施），并确保能附加 correlation context。
- 明确日志输出的目录/命名规则（为后续 history/report 铺路）。

---

### Task 6.3 — 进度追踪增强（ProgressTracker + Periodic Progress Events）

**要做什么**
- 在 Section 5 的“最小事件”基础上，补齐“可用进度”：吞吐、ETA、percent 等（不要求花里胡哨，但要稳定）。:contentReference[oaicite:7]{index=7}

**关键点**
- 事件应支持“周期性推送”（按 batch 完成或固定间隔），否则 UI/CLI 没法展示实时进度。:contentReference[oaicite:8]{index=8}
- 进度计算建议由 `ProgressTracker` 统一管理（避免各处自己算一套）。:contentReference[oaicite:9]{index=9}

**交付物**
- `ProgressTracker` 可在执行过程中持续更新并生成 `JobProgressEvent`。
- reporter 能发出 `jobProgress` 风格事件（字段至少覆盖 rowsProcessed/totalPlanned/throughput/eta）。:contentReference[oaicite:10]{index=10}

---

### Task 6.4 — Run Report 生成器（Machine-readable Run Report）

**要做什么**
- 实现 Run Report 输出（JSON 或目录结构均可），包含：plan 摘要、每表结果、耗时、错误列表。:contentReference[oaicite:11]{index=11}

**关键点**
- report 必须能被机器读（后续 GUI/WebApi 要靠它展示历史与结果）。
- 错误列表必须是“聚合后的可行动信息”，不是一坨 exception dump。:contentReference[oaicite:12]{index=12}

**交付物**
- `RunReport`（或 `JobReport`）模型 + `IRunReportWriter`（或等价组件）。
- 每次 job 结束（Success/Failed/Cancelled）都会落地 report 文件。

---

### Task 6.5 — 历史记录服务（History Service）

**要做什么**
- 提供 run history 的读取与追加能力，支撑 “getHistory” 之类的调用（host 只是来要数据）。:contentReference[oaicite:13]{index=13}

**关键点**
- `IHistoryService` 负责 `GetRecentRunsAsync` 和 `AppendAsync(JobResult)` 这类稳定接口；实现可以基于结构化日志或 report 文件重建。:contentReference[oaicite:14]{index=14}
- 对 IO/解析失败要有策略：至少不应该把“本次 job 结果”吞掉（history 写失败是另一个问题）。:contentReference[oaicite:15]{index=15}

**交付物**
- `IHistoryService` + `LogHistoryService`（或你选的存储实现）。
- `RunSummary` 模型（用于列表展示，而不是完整 report）。:contentReference[oaicite:16]{index=16}

---

### Task 6.6 — Job 级取消语义（Cancellation Semantics End-to-End）

**要做什么**
- 在 orchestrator 边界正确支持 cancellation：一旦取消触发，不再继续任何 DML，并产出“Cancelled”结局（事件 + report）。:contentReference[oaicite:17]{index=17}

**关键点**
- cancellation 必须能穿透到 DataWriter/Runner 等底层（尊重 `CancellationToken`）。
- 取消属于“正常流”，不该被记录成“莫名其妙失败”，但必须可追溯（jobId、已处理行数、耗时）。:contentReference[oaicite:18]{index=18}

**交付物**
- 取消时：发出 JobCompleted（Cancelled）事件 + 落地 report（status=Cancelled）。
- Orchestrator 把 `OperationCanceledException` 映射为你定义的取消类型（例如 `UserCancelledException`），并保持一致错误模型。

---

### Task 6.7 — Section 6 的测试面（Observability & Artifacts Tests）

**要做什么**
- 用自动化测试把 Section 6 的“可观测性可用”锁死，不然以后每改一次执行流程就全靠猜。

**关键点**
- 验收必须覆盖文档要求：  
  - 日志能定位到 job/table/phase  
  - 事件序列符合开始→结束/开始→失败  
  - report 文件落地且字段完整:contentReference[oaicite:19]{index=19}

**交付物**
- Unit Tests：
  - 事件序列：JobStart → (TableStart/TableEnd)* → JobEnd/Fail/Cancelled
  - ProgressTracker：吞吐/ETA 计算稳定（给定输入序列输出一致）
  - RunReport：字段完整性校验（schema-level assertions）
- Integration Tests（轻量）：
  - 跑一个最小 job，验证 report 文件生成 + history 可读回
  - 触发取消，验证无后续 DML + report/status 正确




---


---

# Section 7 — Reliability & Operator Experience（重试、稳定性、可控失败）

### 7.1 核心目标

- 提供基础 resilience，但**不制造重复写入事故**。
    

### 7.2 必须做对的决策点

- **Retry 分类**：
    
    - 仅对“读/探测/元数据查询”等可重试操作生效。
        
    - delete/insert 默认不可重试（除非你能证明幂等并且事务保证一致性）。
        
- **错误分类与回滚边界**：
    
    - 明确哪些错误触发立即 abort，哪些可以降级/跳过（MVP 通常直接 abort）。
        

### 7.3 关键约束

- 禁止“看见超时就重试写入”这种赌博行为。
    
- 不引入复杂恢复（resume）机制作为 MVP。
    

### 7.4 验收标准

- 人为制造瞬时 read 错误：能按策略重试并成功。
    
- 人为制造 write 错误：不重试写入，能清晰报告失败点与回滚结果。
    
- 