# Section 2 (Revised): Configuration & Error Model – Implementation Plan

### 2.0 Shared Background for AI Coder (Read This First)

This section is self-contained on purpose. It gives you the business context, YAML shape, safety rules, and architecture contracts you must respect while implementing `ReportSyncer.Core.Configuration` and the configuration error model. You should not need to open other docs.

#### 2.0.1 Product & Business Goal

* **Product:** ReportSyncer is a desktop tool that performs **on-demand, manual data syncs between SQL Server databases** for the AGV BI environment. It is not a continuous replication service. 
* **Core business goal:** Allow users to refresh dimension tables and historical reporting tables from a source environment (often Production) into target environments (Dev / Test / other Reporting DBs) with **strict safety guardrails**:

  * Source is **read-only**: no `DELETE/UPDATE/TRUNCATE/DROP` against any connection marked as *Source*.
  * Target deletions are **pre-sync, controlled, and heavily guarded**. 
* **Typical scenarios:**

  * Full refresh of dimension tables (wipe target + insert fresh data).
  * Historical data sync for a time window (e.g., last 7 days) with filters.
  * Application DB → Reporting DB ingestion with **context injection** (e.g., inject `CustomerId` into Reporting table).
  * Reporting → Reporting copy (Prod → Dev) with filters and no extra context injection.

Your config model and validation are the gatekeepers that make these scenarios **safe** and **predictable**.

#### 2.0.2 YAML Configuration Shape (What You’re Modeling)

The entire backend behavior is driven by a **single YAML configuration file**. The logical entities are:

1. **Run settings (`run`)**

   * Examples: `dryRun`, `defaultBatchSize`, `deleteChunkSize`, `useTvpIfAvailable`, `etaSmoothing`.
2. **Global safety settings (`safety`)**

   * Examples:

     * `forbidProdToProd`
     * `requireDifferentConnections`
     * `confirmLargeDeletePct` (e.g., 0.8 for 80% threshold)
3. **Schema policy (`schemaPolicy`)**

   * Examples:

     * `onMismatch` (`fail`, `warn`, etc.)
     * `requirePrimaryKey`
     * `allowExtraTargetColumns`
4. **Connections (`connections`)**

   * Each connection has at least:

     * `name` (unique key used by jobs)
     * `connectionString`
     * `environment` (e.g., `Prod`, `Dev`)
     * `type` (`Application` or `Reporting`) used for context-injection rules.
5. **Sync jobs (`syncJobs`)**

   * Each job has:

     * `name` and optional `description`
     * `sourceConnection`, `targetConnection` (referencing `connections`)
     * `parameters` dictionary (e.g., `CustomerId`, `StartDate`, `EndDate`)
     * `tables`: list of **table tasks**
6. **Table task (`tables[]`)**

   * Per-table configuration:

     * `source` table name
     * `target` table name
     * `enabled` flag
     * `preSyncTargetAction` (boolean or enum-like, e.g., “delete before insert”)
     * `allowAllDelete` (safety latch for unscoped deletes)
     * `enableIdentityInsert`
     * `filter` block (time range and/or key filter)
     * `columnMapping` (automap, explicit mappings, added parameter columns)
     * `keys` (business key configuration)
     * `syncOptions` (batch size, TVP usage overrides, etc.)

A canonical YAML example (cut for brevity) looks like: 

* `run` / `safety` / `schemaPolicy` at the top
* `connections:` list with `environment` and `type`
* `syncJobs:` each with `parameters` and `tables`, where each table has `preSyncTargetAction`, `allowAllDelete`, `filter`, `columnMapping`, etc.

Your `SyncConfiguration` root model must be able to represent all of this, strongly typed.

#### 2.0.3 Safety Rules You Must Enforce (Static Side)

Config & validation are responsible for the **static rules** that can be checked without touching the DB:

* **No Prod → Prod sync** if `forbidProdToProd == true`.
* **No self-sync** if source and target connections resolve to the same DB or connection string.
* **Source immutability**: configs must never define a scenario where source is mutated; all delete options apply only to the target.
* **Pre-sync delete guardrails:**

  * If `preSyncTargetAction == true` and `filter == null`, **block the config** unless `allowAllDelete == true`.
  * `allowAllDelete` defaults to **false**.
* **Large delete threshold (`confirmLargeDeletePct`)**

  * When estimated deletes > threshold, the system must require explicit confirmation / safety override. The config validation + later safety layer coordinate this.
* **Context injection invariants:**

  * If `Source.Type == Application` and `Target.Type == Reporting`:

    * The system will **inject a context column** (default `CustomerId`, configurable per job) into the target insert set, using a job parameter (e.g. `CustomerId: 50`).
    * If the source table already contains a column with the same name as the configured context column, this is **ambiguous** and must fail validation / pre-flight with an “Ambiguous Context Column” style error.
  * If `Source.Type == Reporting`:

    * No automatic context-injection; `CustomerId` is treated like any normal column unless config explicitly adds a constant column mapping.

Your config validator will not compute row counts or look at actual DB schema, but it **must** enforce all rules that can be derived purely from YAML + parameters.

#### 2.0.4 Architecture & Layering Constraints (Where Your Code Lives)

* **Project:** `ReportSyncer.Core` is the domain core. Configuration models, YAML loading, config validation, and config-level error types all live under the `ReportSyncer.Core.Configuration` (and related error namespaces). 
* **Toolkits:**

  * `DotNetToolkit.General`: generic helpers (`Guard`, `Result<T>`) you may use.
  * `DotNetToolkit.Logging`: logging abstraction `ILogService`.
  * `DotNetToolkit.Database`: generic DB primitives; **do not** call it from configuration code (config has no DB access). 
* **Hosts:** `ReportSyncer.Console` and future `ReportSyncer.WebApi` are just shells:

  * They resolve config file paths, wire DI, invoke configuration loader and orchestrator, and map exceptions to exit codes / HTTP responses.
  * They **must not** contain YAML parsing or config validation logic.

Your configuration code must:

* Depend only on:

  * `DotNetToolkit.General`
  * `DotNetToolkit.Logging`
  * `YamlDotNet`
* Never:

  * Access DB or schema.

  * Depend on host projects.

  * Depend on `DotNetToolkit.Database`.

#### 2.0.5 Error Model You Plug Into

The core domain error taxonomy is:

* `ConfigurationException`
* `SchemaMismatchException`
* `SafetyViolationException`
* `SyncExecutionException`
* `UserCancelledException` (and an optional `UnexpectedInternalException` at host edge).

For **Section 2**:

* Config loading, parsing, and structural validation must throw **`ConfigurationException`** on:

  * YAML syntax errors.
  * Missing required fields.
  * Invalid enum values.
  * Broken references (unknown connection names, etc.).
* Safety rules that are purely config-driven may raise:

  * `ConfigurationException` **or**
  * `SafetyViolationException` depending on final design of your configuration error model (Task 2.5 clarifies the contract).
* All config-related errors must be **recognizable as configuration errors** so hosts can map them to specific exit codes / HTTP status.

Section 2’s tasks below define exactly how to build:

* The **configuration domain model** (strongly typed).
* The **loader** (YAML → domain model).
* The **validator** (semantic static rules).
* The **override / effective config** layer.
* The **configuration error abstraction** and mapping contract.

This section assumes Section 1 (DotNetToolkit) already exists as a separate infra library and is **only consumed**, never modified here. 

---

### Namespace focus: `ReportSyncer.Core.Configuration` (+ shared domain error types)

Important note in light of Section 1:

* This section **uses** toolkit projects (`DotNetToolkit.General`, `DotNetToolkit.Logging`, `DotNetToolkit.Database` where appropriate) but **must not** push any domain logic down into them. All config & error semantics stay in `ReportSyncer.Core`. 

### Global principles for this section

* AI coder **may**:

  * Introduce helper classes, extension methods, small patterns (factory, value objects, etc.).
  * Add derived safety checks or convenience methods if consistent with the PRD & architecture doc.
* AI coder **must not**:

  * Bypass the validation layer and use raw YAML objects in sync logic.
  * Put DB access, logging, or orchestration in this namespace.
  * Push configuration error types into `DotNetToolkit.*`; domain exceptions live in `ReportSyncer.Core` only.
* AI coder **must**:

  * Respect:

    * PRD: config structure, safety rules, delete behavior, environment rules, context injection rules.
    * Backend architecture doc: configuration responsibility, layering rules, and exception taxonomy. 

---

### Task 2.1 – Define the Configuration Domain Model

**Goal**
Represent the YAML configuration as a strongly-typed, immutable-ish domain model that:

* Captures run settings, safety rules, schema policy, DB connections, sync jobs, table tasks, filters, and mapping.
* Is expressive enough to support all behaviors in the PRD, including:

  * Safety features around deletes and prod/prod scenarios.
  * Context injection between Application → Reporting.
  * Filter combinations (date range + key filters).

**What this task focuses on (business view)**

* Make the YAML config *understandable to code*:

  * One root object representing the entire YAML file (e.g., `SyncConfiguration`).
  * Sub-models for `RunConfig`, `SafetyConfig`, `SchemaPolicyConfig`, `ConnectionConfig`, `SyncJobConfig`, `TableTaskConfig`, `FilterConfig`, `ColumnMappingConfig`, `SyncOptionsConfig`, `KeyConfig`, etc.
* The models must be good enough that later modules (schema, safety, sync) never need to touch raw YAML.

**Inputs**

* PRD sections describing:

  * YAML structure and semantics.
  * Safety and environment rules.
  * Context injection rules.
* Backend architecture doc:

  * Class & interface inventory for `ReportSyncer.Core.Configuration`.
  * How other modules expect to consume the config models. 

**What must be implemented**

* A **root configuration object** (e.g. `SyncConfiguration`) representing the whole YAML file:

  * Holds:

    * `RunConfig` (global runtime options like dry-run, batch sizes, chunk sizes).
    * `SafetyConfig` (forbidProdToProd, requireDifferentConnections, confirmLargeDeletePct).
    * `SchemaPolicyConfig`.
    * A list of `ConnectionConfig`.
    * A list of `SyncJobConfig`, each with table-level instructions.
* Job-level and table-level configuration objects that:

  * Identify which source/target connections to use.
  * Identify source and target tables.
  * Capture pre-sync actions (`preSyncTargetAction`, `allowAllDelete`, `enableIdentityInsert`).
  * Capture filters (`FilterConfig`) combining:

    * Optional date range (`dateColumn`, `startDate`, `endDate`).
    * Optional key filter (`keyColumn`, `value`).
  * Capture table-level overrides (`SyncOptionsConfig`) and mapping (`ColumnMappingConfig`, `AddedColumnMappingConfig`).
* Connection configuration objects that:

  * Represent named connections.
  * Include `environment` (Prod/Dev/etc.) and `type` (Application/Reporting) to drive safety and context injection rules.
* Mapping models:

  * `ColumnMappingConfig` & `AddedColumnMappingConfig` to encode:

    * Automap by name.
    * Explicit column mappings.
    * Constant / parameter-based injected columns (e.g. context column).
* Key configuration models:

  * `KeyConfig` for business keys / composite keys used by dedupe / `WHERE NOT EXISTS` patterns.

**Constraints & freedom**

* Structures must align logically with YAML examples but:

  * You may introduce intermediate value objects / enums to improve clarity and safety.
  * Naming can be adjusted if it better expresses intent, as long as YAML mapping stays aligned.
* Domain models should be mostly immutable from the outside:

  * Use constructors / factory methods for invariants where appropriate.

**Expected behavior / tests (concept level)**

* Given a valid YAML (per spec), the domain model can represent:

  * Multiple connections with different envs and types.
  * Multiple jobs with multiple tables.
  * Per-table safety / pre-sync behavior where applicable.
* Creating configuration objects with obviously invalid state (e.g. empty names, missing required parts) is either:

  * Prevented by type design, or
  * Reliably caught by validation (Task 2.3).

---

### Task 2.2 – Implement Configuration Loading (YAML → Domain)

**Goal**
Transform a YAML configuration file into the domain model from Task 2.1 with:

* Strict parsing.
* Clear failure on malformed or unknown content.
* No semantic validation yet (that’s Task 2.3). This stage is about **structure and types** only.

**What this task focuses on (business view)**

* Users edit a YAML file (or a UI writes it). When the backend reads it:

  * Any typo, unknown field, wrong data type, or broken structure must surface as a **configuration-layer failure** with a clear message, not as a random `NullReferenceException`.
* This is the “compiler front-end” of configuration.

**Inputs**

* YAML schema & examples in PRD / Tech spec.
* Architecture docs on:

  * Where YAML parsing is allowed (`ReportSyncer.Core.Configuration` only).
  * Exception taxonomy and how hosts map `ConfigurationException`. 

**What must be implemented**

* An abstraction for loading configuration:

  * Example: `IConfigurationLoader` with `Task<SyncConfiguration> LoadAsync(string path, CancellationToken ct)`.
* A YAML-based implementation (e.g. `YamlConfigurationLoader`) that:

  * Uses `YamlDotNet` to parse YAML from disk (or a stream).
  * Maps YAML nodes into the domain models defined in Task 2.1.
  * Handles parameter placeholders in fields like filters if required by design.
  * Fails fast on:

    * Syntax errors.
    * Unknown top-level or nested fields (do not silently ignore).
    * Unmappable types (e.g. string where a number is required).
* Logging integration via `ILogService` to log parse failures with useful context (path, approximate location, key name).

**Constraints & freedom**

* Use `YamlDotNet` as the only YAML library.
* You may:

  * Use DTOs that mirror YAML shape and then map DTOs → domain models, or
  * Deserialize directly into domain models (as long as invariants are preserved).
* All errors at this stage must surface as **configuration-layer failures**, not raw IO or YAML parser exceptions:

  * Wrap them into `ConfigurationException` (or a configuration-specific subtype) with a clear message.

**Expected behavior / tests**

* Valid YAML sample loads into `SyncConfiguration` with all expected data present.
* When the YAML structure is broken (indentation, sequence vs mapping issues), loader throws `ConfigurationException` with:

  * Clear indication it’s a parse problem.
* When unknown keys appear, loader fails with:

  * An explicit message that the key is unsupported (to catch typos early).
* No DB access occurs in this layer.

---

### Task 2.3 – Implement Configuration Validation

**Goal**
Check all **static** rules that can be validated without DB access:

* Structural integrity.
* Reference integrity (connections referenced by jobs exist, unique names).
* Safety-related configuration rules that depend only on YAML.
* “Obvious footgun” prevention strictly based on config.

**What this task focuses on (business view)**

* Prevent clearly dangerous or nonsensical configurations from ever reaching pre-flight / execution:

  * Jobs referencing unknown connections.
  * Enabled tables with missing source/target.
  * Pre-sync delete enabled with no filters and `allowAllDelete=false`.
  * Prod→Prod sync when forbidden.
* This is the “semantic checker” of YAML.

**Inputs**

* PRD rules for:

  * Environment combinations and Prod/Prod restrictions.
  * Destructive operations (`preSyncTargetAction`, `allowAllDelete`).
  * Large delete thresholds and safety latches.
* Architecture docs:

  * Relationship between configuration validator and later safety components. 

**What must be implemented**

* A validator component:

  * `IConfigurationValidator` with e.g. `void Validate(SyncConfiguration config)`.
* A concrete `ConfigurationValidator` that:

  * Enforces required fields:

    * Non-empty job names, connection names, table names.
    * At least one `syncJob`, at least one valid `tables[]` entry per job.
  * Validates reference integrity:

    * Each `sourceConnection` and `targetConnection` reference must match a `ConnectionConfig`.
    * Job/table names must be unique where required.
  * Validates environment and safety:

    * Prod→Prod blocked when `safety.forbidProdToProd == true`.
    * Self-sync blocked when `safety.requireDifferentConnections == true` and source/target are the same DB.
    * Configuration that enables `preSyncTargetAction` **without** filter and `allowAllDelete == false` must be rejected.
  * Validates numeric ranges:

    * Example: `confirmLargeDeletePct` must be between 0 and 1.
    * Batch sizes / chunk sizes must be positive.
  * Optionally, performs shallow sanity checks around context injection:

    * E.g., connection type presence where required.
* Error reporting:

  * Either:

    * Aggregate validation errors into a single `ConfigurationException` carrying a list, or
    * Throw on first error but with enough context to fix it quickly.
  * Include information: job name, table index, offending field path.

**Constraints & freedom**

* Validator must NOT:

  * Inspect DB schema.
  * Query row counts.
  * Rely on any `DotNetToolkit.Database` types.
* You may:

  * Introduce helper types like `ConfigurationError` / `ConfigurationErrorCode` to standardize messages.
  * Delegate purely safety-ish checks to `SafetyValidator` later, as long as config-level static rules are still enforced here.

**Expected behavior / tests**

* Valid configuration examples pass validation.
* Broken references (unknown connection names, empty job or table lists) produce clear errors.
* Configurations that imply full-table delete without `allowAllDelete=true` are rejected with an explicit safety message.
* Multiple issues in one file can be surfaced together where practical.

---

### Task 2.4 – Build Effective Configuration / Overrides Logic

**Goal**
Allow hosts (CLI/Web API) to override certain runtime options (like dry-run, batch sizes) without changing the underlying YAML file:

* Compute an **effective runtime view** based on:

  * YAML config.
  * Optional host-provided overrides (CLI flags / API payload).

**What this task focuses on (business view)**

* Users might run:

  * “Same job as usual, but dry-run only.”
  * “Same job but with different batch size for this one run.”
* The YAML file remains the source of truth; overrides are **transient**.

**Inputs**

* PRD:

  * Which fields are allowed to be overridden at run-time (e.g., `dryRun`, some batch sizes).
* Architecture docs:

  * How hosts are expected to pass overrides to core (console arguments, API DTOs).

**What must be implemented**

* A clear model for:

  * `RunOverrides` (or similar), containing only fields that hosts are allowed to override.
  * `EffectiveRunConfig` / `EffectiveConfiguration` representing the merged view.
* Merge rules:

  * Override wins if provided.
  * Otherwise use YAML value.
  * If neither exists and field is required:

    * Either use a documented default.
    * Or throw a config-layer error if spec requires explicit configuration.
* Helper(s):

  * `IEffectiveConfigurationBuilder` or static helper methods to compute the effective configuration that:

    * Consume `SyncConfiguration` and optional `RunOverrides`.
    * Produce a read-only object consumed by orchestrator & sync pipeline.

**Constraints & freedom**

* Overrides must stay localized:

  * Sync & schema layers should consume only the **effective** view, not juggle overrides themselves.
* No direct coupling to CLI or HTTP types:

  * Hosts map CLI / HTTP DTOs → `RunOverrides` and call into core.

**Expected behavior / tests**

* When overrides are given:

  * Effective values reflect the overrides and nothing else is unintentionally changed.
* When overrides are omitted:

  * Effective values exactly match YAML.
* There is no way for downstream core code to “half see” overrides (all or nothing via effective config).

---

### Task 2.5 – Configuration Error Model & Host Mapping Contract

**Goal**
Have a **single, explicit error model** for configuration failures, and a clear contract for how hosts react to them (exit codes / HTTP statuses):

* Make configuration-related errors recognizable and consistent.
* Avoid random generic exceptions leaking out of the core.

**What this task focuses on (business view)**

* When the user messes up YAML or config semantics, they should see:

  * A clean, structured message.
  * A deterministic exit code or HTTP status.
* Hosts must not guess; they inspect exception type / metadata and map.

**Inputs**

* Backend architecture docs:

  * Exception taxonomy.
  * Mapping rules to host behavior (console/Web API).

**What must be implemented**

* A dedicated configuration error abstraction, e.g.:

  * `ConfigurationException` (already planned in docs) possibly extended with:

    * Path/location hint within the YAML (e.g. `syncJobs[1].tables[0].filter.dateColumn`).
    * A collection of structured validation errors:

      * `Code` (e.g. `UnknownConnection`, `MissingRequiredField`, `UnsafeDelete`).
      * `Message` (user-oriented).
      * Optional `JobName`, `TableName`, `FieldPath`.
* Clear internal usage:

  * Loader throws this type for parse / mapping issues.
  * Validator throws this type for semantic issues.
  * Overrides & effective config logic also throw this type for override-related config errors.
  * Any safety-like config checks that are purely static can either produce:

    * `ConfigurationException` with relevant code, or
    * A more specific `SafetyViolationException` if you want to separate “config invalid” from “config blocked by safety”.
* A **documented mapping contract** (XML docs / comments) for the hosts:

  * Console host:

    * Map configuration errors to a specific exit code (e.g. 2).
    * Log them in a user-friendly way (no raw stack traces by default).
  * Web API host:

    * Map configuration errors to HTTP 4xx (e.g. `400 Bad Request`).
    * Return a structured body listing error codes, locations, and messages.

**Constraints & freedom**

* You may:

  * Choose between a single `ConfigurationException` type with internal collection vs multiple specialized exception types, as long as mapping stays simple.
  * Add an error code enum/class to help host map to localized messages in the future.
* Error messages should be user-oriented:

  * “Connection `Report_Prod` is referenced by job `Sync-Reporting-ProdToDev-Filtered` but not defined in `connections`.”
  * Not raw parser or stack trace text.

**Expected behavior / tests**

* Any invalid configuration path (parse, validate, override) surfaces as:

  * The configuration error type (`ConfigurationException`) or, where chosen, `SafetyViolationException` with clear codes.
* Host-level tests (later sections) can:

  * Detect configuration errors by type.
  * Map them consistently to exit codes / HTTP statuses and structured HTTP payloads.


