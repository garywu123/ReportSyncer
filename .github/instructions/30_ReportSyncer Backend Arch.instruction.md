
## Project Layout & Dependencies

This section defines the canonical backend solution layout, responsibilities, and dependency rules for the ReportSyncer backend.  
All backend work **must** conform to this structure.

### 1. Project list & responsibilities

#### 1.1 Business / backend projects

|Project|Type|Category|Responsibility|
|---|---|---|---|
|`ReportSyncer.Core`|Class lib|Business|All backend business logic: configuration models & validation, schema inspection & mapping, safety checks, sync orchestration pipeline, data-writing logic.|
|`ReportSyncer.Console`|EXE|Host|CLI host for batch / ops scenarios: parse args, locate config file, build DI container, configure logging, wire progress output, invoke `ReportSyncer.Core`.|
|`ReportSyncer.WebApi`|Web API EXE|Host|HTTP API host for frontend clients (React/Electron or other). Exposes endpoints for configuration inspection, sync history, job execution, and status. Maps HTTP DTOs to `ReportSyncer.Core` services and handles auth; **no business logic beyond mapping & authorization.**|
|`ReportSyncer.Tests`|Test proj|Tests|Unit and integration tests for `ReportSyncer.Core` and, where useful, `ReportSyncer.WebApi` behavior. Includes DB-backed tests (LocalDB/seed data) for schema/sync operations.|

#### 1.2 Shared toolkit projects

These projects are **domain-agnostic** utilities intended to be reused by multiple applications (not just ReportSyncer).

|Project|Responsibility (high-level)|
|---|---|
|`DotNetToolkit.General`|General-purpose utilities: argument/option parsing helpers, guard clauses, result / option types, small cross-cutting helpers.|
|`DotNetToolkit.Logging`|Logging abstractions and adapters (e.g. `ILogService`), plus basic sinks / formatting helpers. No app-specific logging assumptions.|
|`DotNetToolkit.Database`|Database helpers: connection factory, `IDbContext`, retry policies, lightweight data access primitives, mapping via `IDataMapper<T>`.|

Future toolkits (e.g. `DotNetToolkit.Security`, `DotNetToolkit.Web`) must follow the same rule: reusable and strictly domain-agnostic (see extension rules below).

---

### 2. Allowed / forbidden dependencies

All references **must** follow this normative dependency graph. This is not a description of “current state”; it is the contract the solution must obey.

```text
Allowed references (project → may reference):

  ReportSyncer.Core
    → DotNetToolkit.General
    → DotNetToolkit.Logging
    → DotNetToolkit.Database

  ReportSyncer.Console
    → ReportSyncer.Core
    → DotNetToolkit.General
    → DotNetToolkit.Logging

  ReportSyncer.WebApi
    → ReportSyncer.Core
    → DotNetToolkit.General
    → DotNetToolkit.Logging
    → DotNetToolkit.Database   (for host-level concerns only, never business rules)

  ReportSyncer.Tests
    → ReportSyncer.Core
    → ReportSyncer.Console
    → ReportSyncer.WebApi
    → DotNetToolkit.*

Forbidden references:

  DotNetToolkit.*  ↛  ReportSyncer.*
    (toolkits must never depend on business projects)

  ReportSyncer.Core ↛ ReportSyncer.Console
  ReportSyncer.Core ↛ ReportSyncer.WebApi
    (business logic is independent of specific hosts)

  ReportSyncer.Console ↛ ReportSyncer.WebApi
  ReportSyncer.WebApi  ↛ ReportSyncer.Console
    (hosts do not reference each other)

  Any cross-business-project reference
    beyond those explicitly listed in "Allowed references"
    is forbidden and must not be added without updating this spec.
```

If a reference is not explicitly allowed above, treat it as **forbidden** until this document is updated.

---

### 3. Responsibilities & placement rules

For each project, this section defines what **must** live there and what **must not**. Coding agents must use these rules to decide where to place new classes and to validate dependencies.

#### 3.1 `ReportSyncer.Core` (business logic)

**Contains (must):**
- Domain models & configuration:
    - `SyncConfiguration`, `RunConfig`, `SafetyConfig`, `ConnectionConfig`, `SyncJob`, `TableTask`, etc.
      
- Configuration loading & validation:
    - Configuration loader & validator, including YAML deserialization and rules such as `requireDifferentConnections`, `forbidProdToProd`, `allowAllDelete` guardrails, etc.
      
- Schema & dependency logic:
    - Schema inspection (`ISchemaInspector`, `SqlServerSchemaInspector`), schema mapping (`SchemaMapper`), FK dependency resolver (`IDependencyResolver`).
      
- Safety / permissions:
    - Permission profiling, runtime safety checks (pre-flight), enforcement of guardrails (prod-to-prod, self-sync, full-table delete rules).
      
- Sync orchestration:
    - `ISyncOrchestrator`, `SyncOrchestrator`, `PreFlightValidator`, `TableRunner`, dependency-ordered delete/insert phases, dry-run behavior.
      
- Data access logic **as part of sync execution**:
    - Data writer abstraction & implementation (`IDataWriter`, `SqlDataWriter` or equivalent), SQL generation helpers specifically for sync jobs (delete, insert, TVP), identity insert management.
      
- Observability surface for the domain:
    - Domain-level logging interfaces, progress reporting contracts, history / run summary models (e.g. `RunSummary`, `TableResult`, `WorkEstimator`, `ProgressTracker`).
        

**Must not contain:**
- CLI argument parsing, console color formatting, environment-specific paths, file dialogs.
- HTTP concerns (controllers, routing, middleware, DTOs, authentication attributes).
- Electron/React concepts, IPC wiring, or UI details.
- Hard-coded connection strings or environment detection logic.
- Any reference to:
    - `ReportSyncer.Console`
    - `ReportSyncer.WebApi`
    - Frontend-specific types.

If you are handling process lifetime, HTTP context, command-line arguments, or UI concerns, you are **not** in `ReportSyncer.Core`.

---

#### 3.2 `ReportSyncer.Console` (CLI host)

**Contains (must):**

- Entry point and process lifecycle:
    
    - `Main` method and top-level program wiring.
        
- CLI & configuration bootstrapping:
    
    - Command-line argument parsing.
        
    - Resolution of configuration file path(s) and environment.
        
    - Loading configuration from disk and handing it to `ReportSyncer.Core`.
        
- Dependency injection composition root:
    
    - DI container creation & registration of `ReportSyncer.Core` and `DotNetToolkit.*` services.
        
    - Host-level configuration (e.g. reading config from appsettings / environment variables).
        
- Logging & output wiring:
    
    - Wiring `DotNetToolkit.Logging` to console output, file sinks, etc.
        
    - Mapping domain events / progress notifications to console output.
        
- Process result mapping:
    
    - Mapping domain exceptions / sync outcomes to process exit codes.
        

**Must not contain:**

- Direct SQL/DML against business databases.
    
- Business decision logic (e.g. deciding whether a job is safe, schema matching rules).
    
- Duplication of sync orchestration logic that exists in `ReportSyncer.Core`.
    
- Any HTTP-specific types or behavior (controllers, routing).
    

Anything that looks like “real work” on data belongs in `ReportSyncer.Core`; `ReportSyncer.Console` is wiring, not logic.

---

#### 3.3 `ReportSyncer.WebApi` (HTTP host)

**Contains (must):**

- HTTP host bootstrap:
    
    - Web host startup / `Program` / `Startup` for ASP.NET Core (or equivalent).
        
    - Registration of controllers / endpoints.
        
- API surface & DTOs:
    
    - HTTP request/response models (DTOs) for:
        
        - Configuration inspection and editing.
            
        - Sync job listing and execution.
            
        - Sync history and run details.
            
        - Job status / progress queries.
            
- Mapping & orchestration:
    
    - Adapters that translate HTTP DTOs and route data into `ReportSyncer.Core` services (e.g. `ISyncOrchestrator`, config services).
        
    - Mapping of domain models / summaries to HTTP responses.
        
- Security & authorization:
    
    - AuthN/AuthZ rules, filters, middleware specific to Web API.
        
- Host-level concerns:
    
    - HTTP logging, correlation IDs, request lifetime management.
        
    - Use of `DotNetToolkit.Database` **only for host-specific concerns**, such as Web API’s own persistence (if any), not for sync business rules.
        

**Must not contain:**

- Sync business rules (e.g. pre-flight validation logic, schema comparison logic, data writer logic).
    
- Direct SQL access to AGV/ReportDB for sync purposes (that is `ReportSyncer.Core`’s responsibility).
    
- YAML parsing or configuration validation logic already defined in `ReportSyncer.Core`.
    
- Cross-talk with `ReportSyncer.Console` (no direct references).
    

Controllers may **only** orchestrate calls into `ReportSyncer.Core` and shape HTTP responses. If a controller starts to “decide” how a sync should work, that logic belongs in Core.

---

#### 3.4 `ReportSyncer.Tests` (test project)

**Contains (must):**

- Unit tests for:
    
    - Configuration loader & validator.
        
    - Schema comparison & dependency resolver.
        
    - Safety validator & permissions profiling.
        
    - Sync orchestrator (using mocks), pre-flight validator, data writer SQL generation, progress tracker.
        
- Integration tests:
    
    - DB-backed tests using LocalDB or equivalent seeded databases for:
        
        - Schema inspection.
            
        - FK ordering.
            
        - Delete/insert behavior (batching, chunking, identity insert).
            
- API tests (optional but allowed):
    
    - Tests for `ReportSyncer.WebApi` endpoints using in-memory server or test host.
        

**Must not contain:**

- Production code or shared helper logic that should live in `ReportSyncer.Core` or `DotNetToolkit.*`.
    
- Standalone executables or hosts.
    

Any test-only helpers must stay inside `ReportSyncer.Tests` unless they are clearly reusable, in which case they must be promoted to an appropriate toolkit project with no ReportSyncer dependency.

---

#### 3.5 `DotNetToolkit.General` (general utilities)

**Contains (must):**

- Generic helpers reusable across many applications:
    
    - Guard clauses, small functional helpers, argument/option parsing utilities.
        
    - Generic patterns like result types, retry helpers (if not DB-specific).
        

**Must not contain:**

- Any reference to `ReportSyncer.*`, AGV-specific terms, or sync-specific rules.
    
- Business rules about configuration, safety, schema, or sync.
    

---

#### 3.6 `DotNetToolkit.Logging`

**Contains (must):**
- Logging abstractions:
    - `ILogService` and related interfaces / base implementations
- Concrete logging adapters:
    - Adapters to Serilog or other logging frameworks, configuration helpers for logging.
        
**Must not contain:**
- Any code that knows about `ReportSyncer`’s domain, exception types, or job models.
- Business logic that decides what to log in which situation.
    
All domain-specific logging decisions live in `ReportSyncer.Core`; `DotNetToolkit.Logging` only provides mechanisms.

---

#### 3.7 `DotNetToolkit.Database`

**Contains (must):**

- Low-level DB abstractions & services:
    
    - `IDbConnectionFactory`, `IDbContext`, `IDbCommandWrapper`, `IDataMapper<T>`, and their generic implementations.
        
- Cross-application database configuration:
    
    - `DatabaseSettings`, DI registration helpers (e.g. `AddDatabaseServices`).
        

**Must not contain:**

- Any direct dependency on `ReportSyncer.*`.
    
- Sync-specific SQL (e.g., delete/insert strategies) or concepts like `TableTask`, `SyncJob`, `preSyncTargetAction`, etc.
    
- Hard-coded assumptions about specific database names or schemas.
    

---

### 4. Extension rules for future projects

To prevent architecture drift, any new projects must be classified and wired according to these rules:

1. **Classification is mandatory**
    
    - Every new project must be explicitly classified as:
        
        - **Business/host** (e.g. new host like `ReportSyncer.Service`, `ReportSyncer.Desktop`)
            
        - **Toolkit** (`DotNetToolkit.*`)
            
        - **Tests**
            
    - The classification must be recorded in this section before adding dependencies.
        
2. **Dependency direction rules for new business/host projects**
    
    - New business/host projects:
        
        - May depend on `ReportSyncer.Core`.
            
        - May depend on `DotNetToolkit.*`.
            
        - May depend on test projects **only from test assemblies**, never from production code.
            
    - They must **not**:
        
        - Be referenced by `ReportSyncer.Core`.
            
        - Be referenced by toolkit projects.
            
        - Reference other host projects unless explicitly added as an allowed dependency here.
            
3. **Rules for new toolkit projects**
    
    - New `DotNetToolkit.*` projects:
        
        - Must remain domain-agnostic and reusable in other applications.
            
        - May depend on other `DotNetToolkit.*` projects if it does not introduce domain knowledge.
            
    - They must **never**:
        
        - Reference any `ReportSyncer.*` project.
            
        - Use AGV/ReportSyncer naming or concepts in their public APIs.
            
4. **Test projects**
    
    - New test projects (e.g. `ReportSyncer.Core.Tests`, `ReportSyncer.WebApi.Tests`) may reference:
        
        - Their corresponding production project.
            
        - Other business projects as needed.
            
        - Any `DotNetToolkit.*` project.
            
    - Production projects must **not** depend on test projects.
        
5. **Adding new cross-business dependencies**
    
    - Any new cross-business reference (e.g. from a new host to another host) is **forbidden by default**.
        
    - To allow it, this section must be updated with:
        
        - Rationale for the dependency.
            
        - Exact allowed reference line in the “Allowed references” list.
            

---

With this section, a coding agent can answer deterministically:

- **“Where does this new class go?”**
    - Domain / sync logic → `ReportSyncer.Core`
    - CLI / batch host plumbing → `ReportSyncer.Console`
    - HTTP / API surface → `ReportSyncer.WebApi`
    - Reusable infra → appropriate `DotNetToolkit.*`
        
- **“Is this reference allowed?”**
    - Check the Allowed/Forbidden matrix above. If it’s not listed as allowed, it is forbidden.
- **“Can I add this helper to DotNetToolkit or must it stay in Core?”**
    - If it knows anything about ReportSyncer/AGV/sync semantics, it **must** stay in `ReportSyncer.Core`.
    - If it is purely generic and reusable across apps, it can live in an appropriate `DotNetToolkit.*` project, subject to the rules above.



## Architecture Style & Core Patterns

This section defines the overall backend style for ReportSyncer and the core services in `ReportSyncer.Core`. Coding agents must treat this as the canonical reference when designing classes, flows, and dependencies.

### Library Requirement

All backend work must respect these constraints for third-party dependencies:

- Only use free, permissive OSS licenses (MIT / Apache-2.0 / BSD-3-Clause).
- No GPL / LGPL / AGPL or commercial licenses, even if the project is used inside a company.
- No library is allowed in production code unless it is listed or explicitly approved in this section.

#### Core platform & common infrastructure

- **Target framework**
  - `.NET 9` (`net9.0`) for all backend projects.

- **BCL & Microsoft stack**
  - `Microsoft.Extensions.DependencyInjection`
  - `Microsoft.Extensions.Logging.Abstractions`
  - `Microsoft.Extensions.Options` / `Microsoft.Extensions.Configuration.Abstractions`
  - `Microsoft.Data.SqlClient` as the only SQL Server provider for production code.

These are considered “platform” and may be used across `ReportSyncer.*` and `DotNetToolkit.*` projects.

#### Dependency Injection

- **Container choice**
  - DI **abstractions & registration** use the standard `Microsoft.Extensions.DependencyInjection` APIs.
  - **Concrete container**: `DryIoc` via `DryIoc.Microsoft.DependencyInjection` is the default implementation.
    - Container bootstrap (DryIoc setup) lives **only** in host projects (`ReportSyncer.Console`, future `ReportSyncer.WebApi`).
    - `ReportSyncer.Core` and `DotNetToolkit.*` must not take a direct dependency on DryIoc types.

- **Rules**
  - Constructor injection only, no service locator.
  - New services must be registered through the DI composition root, not with static singletons.

#### Logging

- **Abstraction**
  - `DotNetToolkit.Logging.ILogService` is the only logging abstraction referenced from `ReportSyncer.Core`. :contentReference[oaicite:1]{index=1}  

- **Implementation**
  - **Serilog** is the standard logging implementation:
    - Core packages: `Serilog`, `Serilog.Sinks.Console`, `Serilog.Sinks.File` (JSON output), optionally `Serilog.Sinks.Async`.
    - For Web API host (when added): `Serilog.AspNetCore`.
    - Integration with `ILogService` is done inside `DotNetToolkit.Logging` (e.g. `SerilogLogService` adapter).
  - No other logging frameworks (NLog, log4net, etc.) are allowed in production code.

- **Rules**
  - `ReportSyncer.Core` only depends on `ILogService`.
  - Serilog configuration and sink wiring live in host projects and/or `DotNetToolkit.Logging`, never in domain services.

#### Data access & SQL

- **Provider layer (toolkit)**
  - `DotNetToolkit.Database` is the canonical data access abstraction:
    - Uses `IDbContext`, `IDbCommandWrapper`, `IDataMapper<T>` etc. as already defined.
    - Implementation uses `Microsoft.Data.SqlClient` and `DatabaseSettings` for configuration.

- **ORM / micro-ORM**
  - **No full ORM** (e.g. Entity Framework Core) is allowed in `ReportSyncer.Core` or `DotNetToolkit.*`.
  - **Dapper**:
    - Allowed as an optional micro-ORM **only** for simple read-models / host-level queries (e.g. Web API query endpoints, diagnostics).
    - The core sync pipeline (delete/insert, TVP, identity insert) must use `DotNetToolkit.Database` abstractions and hand-written SQL, not Dapper.
    - No Dapper references in `ReportSyncer.Core.Schema`, `.Sync`, `.Security` namespaces.

- **Rules**
  - All write paths (delete/insert, work estimation) go through `IDbContext` + `SqlServerQueryBuilder` / `SqlDataWriter`.
  - Any new DB library must be explicitly added to this section before use.

#### Serialization & configuration

- **YAML configuration**
  - `YamlDotNet` is the only approved YAML library.
    - Used exclusively in `ReportSyncer.Core.Configuration` for loading `SyncConfiguration`.
    - No YAML parsing in hosts or UI; they call into `IConfigurationLoader` / `IConfigurationProvider`.

- **JSON serialization**
  - **Json.NET (`Newtonsoft.Json`)** is the default JSON serializer for:
    - IPC payloads between Electron frontend and backend (if/when needed).
    - Any JSON-based history or diagnostic output not already handled by Serilog.
  - `System.Text.Json` can be used for internal DTOs if needed, but:
    - Domain models must not depend on a specific JSON library.
    - Only host or infrastructure layers should reference concrete JSON libraries.

- **Rules**
  - No ad-hoc custom serializers sprinkled in domain code.
  - Serialization is treated as an infrastructure concern, wired at the edges (IPC, disk, HTTP).

#### Testing libraries

- **Unit test framework**
  - Preferred: `xUnit` for `ReportSyncer.Tests`.
- **Assertion & mocking**
  - `FluentAssertions` for expressive assertions.
  - `Moq` or `NSubstitute` for mocking (pick one and stick to it across the project).

Test libraries are unrestricted by license (still must be OSS) but must not be referenced from production projects.

#### Library usage by layer

- `ReportSyncer.Core`
  - May reference:
    - `DotNetToolkit.General`, `DotNetToolkit.Logging`, `DotNetToolkit.Database`
    - `YamlDotNet` (through configuration loader only)
  - Must **not** reference:
    - DryIoc, Serilog, Dapper, JSON libraries, or any host/UI frameworks.

- `ReportSyncer.Console` / `ReportSyncer.WebApi`
  - May reference:
    - DryIoc + `DryIoc.Microsoft.DependencyInjection`
    - Serilog + sinks
    - Dapper (for host-only queries)
    - Json.NET / System.Text.Json for IPC/HTTP payloads

- `DotNetToolkit.*`
  - May reference:
    - Microsoft.Extensions.* stack
    - `Microsoft.Data.SqlClient`
  - Must remain domain-agnostic and must not take direct dependencies on DryIoc, Serilog, Dapper, or any `ReportSyncer.*` types.

#### Adding new libraries

Any new third-party dependency must satisfy all of the following:

1. License is permissive (MIT / Apache-2.0 / BSD family) and compatible with commercial use without additional fees.
2. The library is scoped to the appropriate layer (Core vs host vs toolkit) and does not break the dependency rules in *Project Layout & Dependencies*.
3. This section (`Library Requirement`) is updated to document:
   - The library name and version range (if relevant).
   - Its allowed usage (which project(s) / layers, and for what purpose).

If a library is not listed here, treat it as **forbidden** until this section is updated.


### 2.1 Overall style

The backend follows a **layered, domain-centric architecture** with the following characteristics:

- **Single domain core (`ReportSyncer.Core`)** that encapsulates all sync logic:
    
    - Configuration parsing & validation
        
    - Runtime schema & dependency inspection
        
    - Permissions profiling & safety checks
        
    - Sync orchestration & data movement
        
    - Observability models (history, progress, metrics)
        
- **Host shells (`ReportSyncer.Console`, `ReportSyncer.WebApi`)** that:
    
    - Parse input (CLI args or HTTP)
        
    - Build the DI container
        
    - Map host-specific inputs to core services
        
    - Handle process / request lifecycle and environment concerns
        
- **Toolkits (`DotNetToolkit.*`)** that provide reusable, domain-agnostic infrastructure:
    
    - Logging, low-level database abstractions, general utilities
        

All flows that move or validate data **enter the system** via a small set of core services, primarily `ISyncOrchestrator` and `PreFlightValidator`.

### 2.2 High-level orchestration flow

A sync run is modeled as a pipeline coordinated by `ISyncOrchestrator`:

1. **Host entry**
    
    - Console:
        - Parse CLI arguments to determine `configPath`, `jobName`, environment flags.
        - Resolve and instantiate `ISyncOrchestrator` from DI.
            
    - Web API:
        - Accept an HTTP request (e.g. `POST /jobs/run` with job identifier).
        - Map request to a `SyncJob` (or job ID) and call `ISyncOrchestrator`.
            
2. **Configuration loading**
    - `IConfigurationLoader` loads YAML configuration into `SyncConfiguration`.
    - `ConfigurationValidator` validates cross-links, required fields, and base rules.
    - On failure, a `ConfigurationException` is thrown and mapped by the host to user-facing error output.
        
3. **Pre-flight validation (`PreFlightValidator`)**
    
    - Uses other services to perform all “no data moved yet” checks:
        - Connection liveness for source & target.
        - Schema inspection for participating tables via `ISchemaInspector`.
        - Schema comparison & mapping via `SchemaMapper`.
        - Dependency graph building via `IDependencyResolver`.
        - Permissions profiling via `IPermissionProfiler`.
        - Safety checks via `SafetyValidator` (e.g. prod-to-prod, delete policies).
            
    - Aggregates issues into domain exceptions (`SchemaMismatchException`, `SafetyViolationException`, etc.).
        
    - On failure, short-circuits the run; no delete/insert commands are executed.
        
4. **Work estimation & planning**
    
    - `WorkEstimator` counts rows for each table and computes initial work units.
    - `PreFlightValidator` (or the orchestrator) uses dependency graph to derive:
        - Delete order (children → parents).
        - Insert order (parents → children).
    - These plans are stored in in-memory models used by the execution phase.
        
5. **Execution (`ISyncOrchestrator` + `IDataWriter`)**
    
    - For each `TableTask` in the planned order:
        - Execute **delete** phase in reverse dependency order (respecting filters & safety).
        - Execute **insert** phase in forward dependency order.
            
    - `IDataWriter` is responsible for:
        - Building SQL commands via `SqlServerQueryBuilder`.
        - Managing `IDENTITY_INSERT` via `IdentityInsertManager`.
        - Performing batched / chunked operations according to `RunConfig`.
            
6. **Observability, history, and progress**
    
    - Throughout pre-flight and execution:
        - Core services emit structured log events via `DotNetToolkit.Logging` abstractions.
        - `ProgressTracker` tracks rows processed and computes ETA with EMA.
            
    - At the end of a run:
        - `LogHistoryService` reconstructs `RunSummary` and `TableResult` from logs.
        - Hosts expose summaries (console output or Web API endpoints).
            

### 2.3 Core service & pattern catalog

The following table defines the main interfaces and classes that own core behaviors. Coding agents must not invent alternative “entry points” for the same responsibilities.

| Namespace                       | Type      | Name                        | Responsibility                                                                                 | Depends on                                                                                                                                             | Related Tasks      |
| ------------------------------- | --------- | --------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------ |
| ReportSyncer.Core.Configuration | Interface | IConfigurationLoader        | Load YAML configuration into `SyncConfiguration` from a path/stream.                           | DotNetToolkit.General (file helpers), DotNetToolkit.Logging                                                                                            | 10                 |
| ReportSyncer.Core.Configuration | Class     | ConfigurationLoader         | Implements `IConfigurationLoader` using YamlDotNet (or similar) and translates IO/YAML errors. | IConfigurationLoader, DotNetToolkit.Logging                                                                                                            | 10                 |
| ReportSyncer.Core.Configuration | Class     | ConfigurationValidator      | Validate `SyncConfiguration` object for structural & business rules.                           | SyncConfiguration models, DotNetToolkit.Logging                                                                                                        | 10                 |
| ReportSyncer.Core.Schema        | Interface | ISchemaInspector            | Load runtime `TableSchema` / `ColumnSchema` metadata for a given connection & table.           | DotNetToolkit.Database (`IDbContext`), DotNetToolkit.Logging                                                                                           | 20                 |
| ReportSyncer.Core.Schema        | Class     | SqlServerSchemaInspector    | SQL Server implementation of `ISchemaInspector` using system catalogs.                         | ISchemaInspector, DotNetToolkit.Database                                                                                                               | 20                 |
| ReportSyncer.Core.Schema        | Class     | SchemaMapper                | Compare source & target `TableSchema` and produce final column mapping per policy.             | TableSchema models, configuration models                                                                                                               | 20                 |
| ReportSyncer.Core.Schema        | Interface | IDependencyResolver         | Build and query FK-based dependency graphs used for execution ordering.                        | DotNetToolkit.Database, DotNetToolkit.Logging                                                                                                          | 20                 |
| ReportSyncer.Core.Schema        | Class     | SqlServerDependencyResolver | SQL Server implementation of `IDependencyResolver` using `sys.foreign_keys` and related views. | IDependencyResolver, DotNetToolkit.Database                                                                                                            | 20                 |
| ReportSyncer.Core.Security      | Class     | PermissionsProfile          | Holds probed permissions for a table (delete/insert/identity insert).                          | — (pure model)                                                                                                                                         | 30                 |
| ReportSyncer.Core.Security      | Interface | IPermissionProfiler         | Probe runtime DB permissions for delete/insert/IDENTITY_INSERT using safe test operations.     | DotNetToolkit.Database, DotNetToolkit.Logging                                                                                                          | 30                 |
| ReportSyncer.Core.Security      | Class     | SqlServerPermissionProfiler | SQL Server implementation of `IPermissionProfiler`.                                            | IPermissionProfiler, DotNetToolkit.Database                                                                                                            | 30                 |
| ReportSyncer.Core.Security      | Class     | SafetyValidator             | Centralize all runtime safety checks (prod-to-prod, self-sync, delete policies).               | PermissionsProfile, configuration models, DotNetToolkit.Logging                                                                                        | 30                 |
| ReportSyncer.Core.Sync          | Interface | INamedDbContextFactory      | Create named DB contexts based on `ConnectionConfig` for use by orchestrator and data writer.  | DotNetToolkit.Database (`IDbContextFactory`), configuration models                                                                                     | 40, 50             |
| ReportSyncer.Core.Sync          | Class     | NamedDbContextFactory       | Implementation of `INamedDbContextFactory`.                                                    | INamedDbContextFactory, DotNetToolkit.Database                                                                                                         | 40, 50             |
| ReportSyncer.Core.Sync          | Class     | PreFlightValidator          | Execute pre-flight pipeline for a `SyncJob` and aggregate validation failures.                 | IConfigurationLoader, ConfigurationValidator, ISchemaInspector, SchemaMapper, IDependencyResolver, IPermissionProfiler, SafetyValidator, WorkEstimator | 20, 30, 40, 50, 60 |
| ReportSyncer.Core.Sync          | Interface | ISyncOrchestrator           | Primary entry point to execute sync jobs; coordinates pre-flight and execution phases.         | PreFlightValidator, IDataWriter, INamedDbContextFactory, ProgressTracker, DotNetToolkit.Logging                                                        | 40–60              |
| ReportSyncer.Core.Sync          | Class     | SyncOrchestrator            | Implementation of `ISyncOrchestrator` that executes the step pipeline for each `SyncJob`.      | ISyncOrchestrator, configuration models, SyncJob, TableTask, IDataWriter                                                                               | 40–60              |
| ReportSyncer.Core.Sync          | Interface | IDataWriter                 | Provide delete/insert operations for table data using batching and identity insert management. | DotNetToolkit.Database, SqlServerQueryBuilder, IdentityInsertManager, DotNetToolkit.Logging                                                            | 50                 |
| ReportSyncer.Core.Sync          | Class     | SqlServerQueryBuilder       | Encapsulate SQL string generation for select/delete/insert & TVP support.                      | DotNetToolkit.Database                                                                                                                                 | 50                 |
| ReportSyncer.Core.Sync          | Class     | IdentityInsertManager       | Manage `IDENTITY_INSERT` ON/OFF scope safely for a table.                                      | DotNetToolkit.Database, DotNetToolkit.Logging                                                                                                          | 50                 |
| ReportSyncer.Core.Sync          | Class     | SqlDataWriter               | SQL Server implementation of `IDataWriter` using query builder & identity manager.             | IDataWriter, SqlServerQueryBuilder, IdentityInsertManager, INamedDbContextFactory                                                                      | 50                 |
| ReportSyncer.Core.Observability | Class     | RunSummary                  | Summary of a completed job (outcome, duration, metrics).                                       | — (pure model)                                                                                                                                         | 60                 |
| ReportSyncer.Core.Observability | Class     | TableResult                 | Per-table metrics for a job run.                                                               | — (pure model)                                                                                                                                         | 60                 |
| ReportSyncer.Core.Observability | Class     | WorkEstimator               | Estimate initial row counts and total work units before execution.                             | DotNetToolkit.Database, INamedDbContextFactory, DotNetToolkit.Logging                                                                                  | 40, 60             |
| ReportSyncer.Core.Observability | Class     | ProgressTracker             | Track rows processed and compute ETA using EMA.                                                | WorkEstimator, DotNetToolkit.Logging                                                                                                                   | 40, 60             |
| ReportSyncer.Core.Observability | Class     | LogHistoryService           | Read structured logs and reconstruct `RunSummary` collection for history views.                | DotNetToolkit.Logging, history models                                                                                                                  | 60                 |

The coding agents must extend this table when new core services are introduced. New “important behaviors” from the Minimal Doc must not be implemented without an explicit owner row in this catalog.

### 2.4 Domain exceptions and service responsibilities

Domain exceptions are defined in `ReportSyncer.Core` and used as the only user-facing error categories:

- `ConfigurationException`
- `SchemaMismatchException`
- `SafetyViolationException`
- `SyncExecutionException`
    

Each service above is responsible for throwing **only** the relevant exception type(s):
- `IConfigurationLoader`, `ConfigurationLoader`, `ConfigurationValidator`  
    → Throw `ConfigurationException` on invalid YAML, missing config, or semantic config errors.
    
- `ISchemaInspector`, `SqlServerSchemaInspector`, `SchemaMapper`, `IDependencyResolver`, `SqlServerDependencyResolver`  
    → Throw `SchemaMismatchException` when source/target schemas cannot be mapped, or dependency graph is inconsistent.
    
- `IPermissionProfiler`, `SqlServerPermissionProfiler`, `SafetyValidator`  
    → Throw `SafetyViolationException` when guardrails or runtime safety checks fail.
    
- `INamedDbContextFactory`, `IDataWriter`, `SqlDataWriter`, `SyncOrchestrator`, `PreFlightValidator`, `WorkEstimator`, `ProgressTracker`, `LogHistoryService`  
    → Throw `SyncExecutionException` for runtime failures during orchestration, DB operations, work estimation, or history reconstruction.
    

Hosts (`ReportSyncer.Console`, `ReportSyncer.WebApi`) must **not** leak raw DB/IO exceptions directly. They translate them via these domain exceptions and present user-friendly messages based only on this error model.

### 2.5 Test seams (high-level)

This architecture deliberately exposes seams for unit and integration testing:

- **Pure domain / configuration tests**
    
    - `ConfigurationValidator`, `SchemaMapper`, `SafetyValidator`, dependency ordering logic.
        
- **DB-backed integration tests**
    
    - `SqlServerSchemaInspector`, `SqlServerDependencyResolver`, `SqlServerPermissionProfiler`, `SqlDataWriter`, `WorkEstimator`.
        
- **Orchestration tests**
    
    - `SyncOrchestrator` and `PreFlightValidator` via mocks of individual services.
        
- **History & progress tests**
    
    - `LogHistoryService`, `ProgressTracker`, and `WorkEstimator` together to validate ETA and history reconstruction.
        

A separate “Test Surfaces & Seams” section will expand this into a test matrix, but coding agents must treat the interfaces listed in 2.3 as primary test entry points.



---

## Class & Interface Inventory by Namespace

> This table assumes the domain exceptions are defined in the Error Model section:  
> `ConfigurationException`, `SchemaMismatchException`, `SafetyViolationException`, `SyncExecutionException`, plus more specific ones where needed.

#### Legend for “Related Tasks”

- **Task 10**: Configuration Loader & Validator
- **Task 20**: Schema Inspector & Dependency Resolver
- **Task 30**: Permissions Profiler & Safety Checker
- **Task 40**: Sync Orchestrator & Pre-flight Validator
- **Task 50**: Data Writer (Delete & Insert)
- **Task 60**: Logging, History, Progress & ETA
    

---

#### `ReportSyncer.Core.Configuration`

|Namespace|Type|Name|Responsibility|Depends on|Related Tasks|
|---|---|---|---|---|---|
|ReportSyncer.Core.Configuration|Class|SyncConfiguration|Root configuration model representing the YAML config: run settings, safety, schema policy, connections, and sync jobs. Key properties mirror Minimal Doc (`RunConfig`, `SchemaPolicyConfig`, `SafetyConfig`, `ConnectionConfig[]`, `SyncJobConfig[]`). Throws `ConfigurationException` only when constructed/validated programmatically.|–|Task 10|
|ReportSyncer.Core.Configuration|Class|RunConfig|Represents global runtime settings (`dryRun`, `defaultBatchSize`, `deleteChunkSize`, `etaSmoothing`, `useTvpIfAvailable`). Used read-only by orchestrator, data writer, and progress estimation. No behavior besides basic validation. Throws `ConfigurationException` from validator only.|–|Task 10, 40, 50, 60|
|ReportSyncer.Core.Configuration|Class|SchemaPolicyConfig|Represents schema policy (`onMismatch`, `requirePrimaryKey`, `allowExtraTargetColumns`). Consumed by `SchemaMapper` / `SchemaPolicyEvaluator`. Throws `ConfigurationException` on invalid combinations (e.g. unknown `onMismatch`).|–|Task 10, 20|
|ReportSyncer.Core.Configuration|Class|SafetyConfig|Represents global guardrails (`requireDifferentConnections`, `forbidProdToProd`, `confirmLargeDeletePct`). Evaluated by `SafetyValidator`. Throws `ConfigurationException` for invalid ranges (e.g. confirmLargeDeletePct not in [0,1]).|–|Task 10, 30|
|ReportSyncer.Core.Configuration|Class|ConnectionConfig|Configuration for a named connection (`name`, `connectionString`, `environmentTag` or equivalent). Used by `INamedDbContextFactory` and safety checks. Throws `ConfigurationException` when missing required fields.|–|Task 10, 30, 40|
|ReportSyncer.Core.Configuration|Class|SyncJobConfig|Represents a job (`name`, `description`, `sourceConnection`, `targetConnection`, `parameters`, `TableTaskConfig[]`). Used as the job definition by orchestrator. Validation catches missing connections, parameter issues. Throws `ConfigurationException`.|–|Task 10, 40|
|ReportSyncer.Core.Configuration|Class|TableTaskConfig|Config for a single table sync (`source`, `target`, `enabled`, `preSyncTargetAction`, `allowAllDelete`, `enableIdentityInsert`, `filter`, `syncOptions`, `columnMapping`, `keys`, `typeCoercion`). Forms the core of per-table logic. Invalid combinations result in `ConfigurationException` or `SafetyViolationException` during validation.|–|Task 10, 20, 30, 40, 50|
|ReportSyncer.Core.Configuration|Class|FilterConfig|Represents optional table filter (either null, key-based filter like `CustomerId`, or date range with `dateColumn`, `startDate`, `endDate`). Used by SQL builder and safety logic to detect scoped vs unscoped deletes. Throws `ConfigurationException` on invalid config.|–|Task 10, 20, 30, 50|
|ReportSyncer.Core.Configuration|Class|SyncOptionsConfig|Per-table overrides (`batchSize`, `multiRowInsert`, table-level `useTvpIfAvailable` etc.). Read by data writer to tune batching strategy. Throws `ConfigurationException` if values are invalid.|–|Task 10, 50|
|ReportSyncer.Core.Configuration|Class|ColumnMappingConfig|Defines mapping strategy (`automapByName`, explicit mappings, `AddedColumnMappingConfig[]` for injected constants/parameters). Used by `SchemaMapper` & SQL builder to produce final column list. Throws `ConfigurationException` on inconsistent mappings.|–|Task 10, 20, 50|
|ReportSyncer.Core.Configuration|Class|AddedColumnMappingConfig|Represents a single added column (target column name + literal or parameter placeholder like `{CustomerId}`). Used by data writer to inject constant/parameter values. Throws `ConfigurationException` if value placeholder cannot be resolved.|–|Task 10, 50|
|ReportSyncer.Core.Configuration|Class|KeyConfig|Config for keys (`businessKey[]`, `compositeKey[]`). Used to drive dedupe / no-op behavior (`INSERT ... WHERE NOT EXISTS`). Incorrect/empty configuration raises `ConfigurationException` or drives no-op logic.|–|Task 10, 20, 50|
|ReportSyncer.Core.Configuration|Class|TypeCoercionConfig|Config for type coercion policy (`policy = fail|mapCompatible`). Delegated to` SchemaMapper`to enforce coercion rules. Throws`ConfigurationException` if unsupported policy.|–|
|ReportSyncer.Core.Configuration|Interface|IConfigurationLoader|Loads `SyncConfiguration` from a source (file or stream). Key methods: `Task<SyncConfiguration> LoadAsync(string path, CancellationToken ct)`. Throws `ConfigurationException` on IO / parsing failures.|DotNetToolkit.General (file IO helpers), `DotNetToolkit.Logging.ILogService`|Task 10|
|ReportSyncer.Core.Configuration|Class|YamlConfigurationLoader|Concrete loader using YamlDotNet (or equivalent) to read from YAML file into `SyncConfiguration`. Also resolves parameter placeholders where needed. Throws `ConfigurationException` for syntax errors / missing sections.|`IConfigurationLoader`, `ILogService`|Task 10|
|ReportSyncer.Core.Configuration|Interface|IConfigurationValidator|Validates a `SyncConfiguration` against structural and business rules. Key methods: `void Validate(SyncConfiguration config)`. Throws `ConfigurationException` or `SafetyViolationException` for invalid guardrails (e.g. preSync + no filter + allowAllDelete=false).|–|Task 10, 30|
|ReportSyncer.Core.Configuration|Class|ConfigurationValidator|Concrete validator implementing all rules from requirements: existing connections, unique names, safety preconditions, schema policy sanity, etc. Responsible for early, deterministic failure. Throws `ConfigurationException` / `SafetyViolationException` with clear messages.|`IConfigurationValidator`, `SafetyConfig`, `SafetyValidator` (optional reuse)|Task 10, 30|
|ReportSyncer.Core.Configuration|Interface|IConfigurationProvider|High-level facade providing a validated config to callers. Key methods: `Task<SyncConfiguration> LoadAndValidateAsync(string path, CancellationToken ct)`. Throws `ConfigurationException` / `SafetyViolationException` only, hiding lower-level details.|`IConfigurationLoader`, `IConfigurationValidator`, `ILogService`|Task 10, 40|

---

#### `ReportSyncer.Core.Schema`

|Namespace|Type|Name|Responsibility|Depends on|Related Tasks|
|---|---|---|---|---|---|
|ReportSyncer.Core.Schema|Class|TableSchema|Runtime model of a table (schema name, table name, columns, PK definition, indexes). Used by `SchemaMapper` and dependency resolver. Does not throw; purely a data holder.|–|Task 20|
|ReportSyncer.Core.Schema|Class|ColumnSchema|Runtime model of a column (name, data type, length/precision, nullability, identity flag). Used for compatibility checks and identity insert decisions.|–|Task 20, 50|
|ReportSyncer.Core.Schema|Class|ForeignKeyRelation|Represents a single FK relationship in the target DB (parent table, child table, column mappings). Inputs into DAG for ordering deletes/inserts. Inconsistent state is caught by its factory / inspector.|–|Task 20, 40|
|ReportSyncer.Core.Schema|Interface|ISchemaInspector|Abstraction for retrieving schema metadata from a given database connection. Key methods: `Task<TableSchema> GetTableSchemaAsync(string tableName, CancellationToken ct)`, `Task<IReadOnlyList<ForeignKeyRelation>> GetForeignKeysAsync(CancellationToken ct)`. Throws `SchemaMismatchException` or `SyncExecutionException` on DB access failures.|`DotNetToolkit.Database.Abstractions.IDbContext`|Task 20, 40|
|ReportSyncer.Core.Schema|Class|SqlServerSchemaInspector|SQL Server implementation of `ISchemaInspector` using system views (`sys.tables`, `sys.columns`, `sys.types`, `sys.identity_columns`, `sys.foreign_keys`). Responsible for hydration of `TableSchema` / `ForeignKeyRelation`. Throws `SyncExecutionException` on unexpected SQL errors.|`ISchemaInspector`, `IDbContext`, `ILogService`|Task 20|
|ReportSyncer.Core.Schema|Interface|ISchemaMapper|Abstraction to compare and map source/target schemas with respect to `TableTaskConfig` & `SchemaPolicyConfig`. Key method: `SchemaMappingResult CompareAndMap(TableSchema source, TableSchema target, TableTaskConfig table)`. Throws `SchemaMismatchException` where policy says `fail`, or returns warnings.|`SchemaPolicyConfig`, `ColumnMappingConfig`, `TypeCoercionConfig`|Task 20, 40, 50|
|ReportSyncer.Core.Schema|Class|SchemaMapper|Concrete implementation of `ISchemaMapper`. Applies automap-by-name, added columns, key config, type coercion policy, and `allowExtraTargetColumns`. Produces final column lists + mapping metadata for the data writer. Throws `SchemaMismatchException`.|`ISchemaMapper`, `ILogService`|Task 20, 50|
|ReportSyncer.Core.Schema|Class|SchemaMappingResult|Immutable result object from `SchemaMapper`, containing projected source columns, target columns, added columns, key columns, and a list of warnings. Used by `TableRunner` & SQL builder.|–|Task 20, 50|
|ReportSyncer.Core.Schema|Interface|IDependencyResolver|Resolves FK-based table execution order. Key methods: `ExecutionPlan BuildExecutionPlan(IEnumerable<TableTaskConfig> tables, IEnumerable<ForeignKeyRelation> fks)`, with properties for insert-order and delete-order sequences. Throws `SchemaMismatchException` or `SyncExecutionException` for cycles / missing tables.|`ForeignKeyRelation`, `TableTaskConfig`|Task 20, 40|
|ReportSyncer.Core.Schema|Class|SqlServerDependencyResolver|SQL Server-focused resolver that builds a DAG from FK relations, performs topological sort, and detects cycles (A→B→A). Returns an `ExecutionPlan` with insert-order (parents→children) and delete-order (children→parents). Throws `SchemaMismatchException` on cycle detection.|`IDependencyResolver`, `ILogService`|Task 20, 40|
|ReportSyncer.Core.Schema|Class|ExecutionPlan|Encapsulates ordered lists of tables for deletion & insertion phases for a given job. Used by `SyncOrchestrator` to drive table execution. Treated as immutable once created.|–|Task 20, 40|
|ReportSyncer.Core.Schema|Interface|ISchemaPolicyEvaluator|Optional helper abstraction that encapsulates application of `SchemaPolicyConfig` to mismatches. Key methods: `void EnsureCompatible(TableSchema source, TableSchema target, TableTaskConfig table)`. Throws `SchemaMismatchException` or emits warnings.|`SchemaPolicyConfig`|Task 20|
|ReportSyncer.Core.Schema|Class|SchemaPolicyEvaluator|Concrete `ISchemaPolicyEvaluator` used internally by `SchemaMapper` to handle `fail`, `warn`, `mapCompatible`. Centralizes the rule logic so it isn’t duplicated.|`ISchemaPolicyEvaluator`|Task 20|

---

#### `ReportSyncer.Core.Security`

|Namespace|Type|Name|Responsibility|Depends on|Related Tasks|
|---|---|---|---|---|---|
|ReportSyncer.Core.Security|Class|PermissionsProfile|Result model describing probed permissions for a specific table or connection (`CanDelete`, `CanInsert`, `CanSetIdentityInsert`). Used by pre-flight to decide if a job can proceed.|–|Task 30, 40|
|ReportSyncer.Core.Security|Interface|IPermissionProfiler|Abstraction for probing runtime permissions on a given table / schema. Key methods: `Task<PermissionsProfile> ProbeTablePermissionsAsync(string tableName, CancellationToken ct)`. Uses safe, no-op operations (DELETE TOP(0), SET IDENTITY_INSERT) in a transaction. Throws `SyncExecutionException` for unexpected DB failures.|`IDbContext`, `ILogService`|Task 30|
|ReportSyncer.Core.Security|Class|SqlServerPermissionProfiler|Concrete `IPermissionProfiler` for SQL Server. Implements the specific probing queries & error handling, translates SQL exceptions into permission flags without mutating data.|`IPermissionProfiler`, `IDbContext`|Task 30|
|ReportSyncer.Core.Security|Interface|ISafetyValidator|Encapsulates all runtime safety rules that can cause a job to be blocked. Key methods: `void ValidateConfiguration(SyncConfiguration config)`, `void ValidateJob(SyncJobConfig job, ConnectionConfig source, ConnectionConfig target)`, `void ValidateTableTask(SyncJobContext jobCtx, TableTaskConfig table, EstimatedDeleteStats stats)`. Throws `SafetyViolationException`.|`SafetyConfig`, `PermissionsProfile`, `RunConfig`|Task 10, 30, 40|
|ReportSyncer.Core.Security|Class|SafetyValidator|Concrete `ISafetyValidator`. Implements: same-connection guard (`requireDifferentConnections`), prod-to-prod guard (`forbidProdToProd`), preSync full-table delete guard (`allowAllDelete` requirement), large delete confirmation threshold (`confirmLargeDeletePct`). Central authority for all safety-related decisions. Throws `SafetyViolationException` with clear codes.|`ISafetyValidator`, `IEnvironmentClassifier`, `ILogService`|Task 10, 30, 40|
|ReportSyncer.Core.Security|Interface|IEnvironmentClassifier|Classifies connections as “prod”, “dev”, etc. based on name or config tags, used to implement `forbidProdToProd`. Key method: `EnvironmentKind Classify(ConnectionConfig connection)`. Throws `ConfigurationException` for invalid classification config.|`ConnectionConfig`|Task 30|
|ReportSyncer.Core.Security|Class|NameBasedEnvironmentClassifier|Concrete `IEnvironmentClassifier` that classifies based on naming convention (e.g. `_Prod`, `_Dev`) or explicit `EnvironmentTag` in config. Keeps prod/dev detection out of business logic.|`IEnvironmentClassifier`|Task 30|
|ReportSyncer.Core.Security|Class|EstimatedDeleteStats|Value object capturing estimated delete counts and percentage of table affected, used for large-delete safety checks and dry-run reporting.|`WorkEstimator` (from Observability)|Task 30, 60|
|ReportSyncer.Core.Security|Class|SyncJobContext|Lightweight per-job context that binds together `SyncJobConfig`, resolved connections, permissions, and environment classification, so validators don’t need to re-resolve everything. Passed into safety and preflight logic.|`SyncJobConfig`, `ConnectionConfig`, `PermissionsProfile`|Task 30, 40|

---

#### `ReportSyncer.Core.Sync`

|Namespace|Type|Name|Responsibility|Depends on|Related Tasks|
|---|---|---|---|---|---|
|ReportSyncer.Core.Sync|Interface|ISyncOrchestrator|Main entrypoint for executing a sync job. Key methods: `Task<JobResult> RunJobAsync(string jobName, CancellationToken ct)` and `Task<JobResult> RunJobAsync(SyncJobConfig job, CancellationToken ct)`. Guarantees pre-flight before any mutation, honors `dryRun`, cancellation, and structured status reporting. Throws `ConfigurationException`, `SafetyViolationException`, `SyncExecutionException`.|`IConfigurationProvider`, `PreFlightValidator`, `ITableRunner`, `ISafetyValidator`, `ExecutionPlan`, `IJobProgressReporter`, `ILogService`|Task 40–60|
|ReportSyncer.Core.Sync|Class|SyncOrchestrator|Concrete implementation of `ISyncOrchestrator`. Responsibilities: resolve job, run pre-flight, fetch execution plan, orchestrate delete phase (reverse order) then insert phase (forward order), collect metrics, and emit final `JobResult`. It is the only class that owns the high-level “run job” lifecycle. Throws domain exceptions unchanged so hosts can handle them consistently.|`ISyncOrchestrator`, `PreFlightValidator`, `ITableRunner`, `ExecutionPlan`, `WorkEstimator`, `ProgressTracker`|Task 40–60|
|ReportSyncer.Core.Sync|Class|JobResult|Immutable summary of a completed job (status: success/failure/cancelled, duration, per-table results, rows deleted/inserted, dry-run flag). Returned by `ISyncOrchestrator` and used by history/logging.|`TableResult` (Observability)|Task 40, 60|
|ReportSyncer.Core.Sync|Class|PreFlightValidator|Aggregates configuration validation, schema inspection, schema mapping, dependency resolution, permissions profiling, safety checks, and initial row-count estimations. Key method: `Task<PreFlightResult> ExecuteChecksAsync(SyncJobConfig job, CancellationToken ct)`. Throws specific domain exceptions; if it fails, `SyncOrchestrator` must not start mutation.|`IConfigurationValidator`, `ISchemaInspector`, `ISchemaMapper`, `IDependencyResolver`, `IPermissionProfiler`, `ISafetyValidator`, `WorkEstimator`, `ILogService`|Task 20, 30, 40, 60|
|ReportSyncer.Core.Sync|Class|PreFlightResult|Contains validated config, `ExecutionPlan`, schema mapping results per table, permission profiles, estimated work (`rowsToDelete/insert`), and a flag indicating if run is effectively a no-op. Used by orchestrator & table runners.|`ExecutionPlan`, `SchemaMappingResult`, `PermissionsProfile`, `EstimatedDeleteStats`|Task 20, 30, 40, 60|
|ReportSyncer.Core.Sync|Interface|ITableRunner|Abstraction for executing a single `TableTaskConfig` according to pre-flight metadata. Key method: `Task<TableResult> RunAsync(TableExecutionContext ctx, CancellationToken ct)`. Handles both dry-run and real execution, but never orchestrates multiple tables.|`IDataWriter`, `SchemaMappingResult`, `TableTaskConfig`, `PermissionsProfile`, `ILogService`|Task 40, 50|
|ReportSyncer.Core.Sync|Class|TableRunner|Concrete `ITableRunner`. Responsibilities: apply delete / insert phases for a single table in the correct order, invoke `IDataWriter.DeleteAsync` / `InsertAsync`, manage identity insert scope, apply filters & parameters, collect metrics, and emit per-table progress events. Translates low-level DB errors into `SyncExecutionException` while preserving context (`JobId`, `TableName`).|`ITableRunner`, `IDataWriter`, `IdentityInsertManager`, `SqlServerQueryBuilder`, `ProgressTracker`|Task 40, 50, 60|
|ReportSyncer.Core.Sync|Interface|IDataWriter|Abstraction for low-level delete/insert operations against SQL Server. Key methods: `Task<int> DeleteAsync(DeleteCommandContext ctx, CancellationToken ct)`, `Task<int> InsertAsync(InsertCommandContext ctx, CancellationToken ct)`. Must support chunked deletes and batched inserts, optionally via TVPs, and cooperate with identity insert scopes. Throws `SyncExecutionException` for DB-level failures.|`IDbContext`, `SqlServerQueryBuilder`, `SchemaMappingResult`|Task 50|
|ReportSyncer.Core.Sync|Class|SqlDataWriter|Concrete `IDataWriter` for SQL Server using `DotNetToolkit.Database` abstractions. Implements chunked-delete loop (`DELETE TOP(@ChunkSize)`) and batched multi-row inserts or TVP strategy depending on config. Integrates with `IdentityInsertManager` when needed and reports row counts to `ProgressTracker`.|`IDataWriter`, `IDbContext`, `SqlServerQueryBuilder`, `IdentityInsertManager`, `RunConfig`|Task 50, 60|
|ReportSyncer.Core.Sync|Class|DeleteCommandContext|Context value object describing a delete operation (target table, filter expression/parameters, chunk size, safety metadata). Keeps SQL generator and data writer stateless.|`TableTaskConfig`, `FilterConfig`, `RunConfig`|Task 50|
|ReportSyncer.Core.Sync|Class|InsertCommandContext|Context for insert operations (source select, target table, columns mapping, batch size, useTvp flag, identity insert required). Used by `SqlDataWriter` to generate proper commands.|`SchemaMappingResult`, `SyncOptionsConfig`, `RunConfig`, `KeyConfig`, `TypeCoercionConfig`|Task 50|
|ReportSyncer.Core.Sync|Class|IdentityInsertManager|Utility that manages `SET IDENTITY_INSERT <table> ON/OFF` via RAII/`IDisposable` pattern. Key method: `Task<IdentityInsertScope> AcquireScopeAsync(string tableName, CancellationToken ct)`. Ensures `OFF` is always executed even on error. Throws `SyncExecutionException` and logs strongly if cleanup fails.|`IDbContext`, `ILogService`|Task 50|
|ReportSyncer.Core.Sync|Class|IdentityInsertScope|Disposable scope returned by `IdentityInsertManager`. On dispose, ensures `SET IDENTITY_INSERT OFF` has executed. Used by `TableRunner` / `SqlDataWriter`.|`IdentityInsertManager`|Task 50|
|ReportSyncer.Core.Sync|Class|SqlServerQueryBuilder|Generates parameterized SQL strings and `IDbCommandWrapper` setup for common patterns: `SELECT` with optional filter, chunked `DELETE`, batched multi-row `INSERT`, TVP-based inserts. No side effects. Throws `ConfigurationException` for unsupported mapping or invalid config.|`SchemaMappingResult`, `FilterConfig`, `KeyConfig`, `RunConfig`, `DotNetToolkit.Database.Abstractions.IDbCommandWrapper`|Task 50|
|ReportSyncer.Core.Sync|Class|TableExecutionContext|Aggregates everything `TableRunner` needs: table config, mapping result, permissions, estimated work, connection factory/context, plus job & table identifiers for logging.|`TableTaskConfig`, `SchemaMappingResult`, `PermissionsProfile`, `IDbContext`, `RunConfig`|Task 40–50|

---

#### `ReportSyncer.Core.Observability`

|Namespace|Type|Name|Responsibility|Depends on|Related Tasks|
|---|---|---|---|---|---|
|ReportSyncer.Core.Observability|Interface|IJobProgressReporter|Abstraction the core uses to report job-level and table-level progress, independent of concrete sinks (console, IPC, file). Key methods: `void ReportJobProgress(JobProgressEvent evt)`, `void ReportLog(JobLogEvent evt)`, `void ReportJobCompleted(JobCompletedEvent evt)`. No exceptions besides argument validation.|`RunConfig`|Task 40, 60|
|ReportSyncer.Core.Observability|Class|JobProgressEvent|DTO carrying progress metrics: job id, table name, phase (`PreFlight/Delete/Insert`), rows processed so far, total planned rows, ETA, throughput. Emitted by `ProgressTracker` / orchestrator.|–|Task 60|
|ReportSyncer.Core.Observability|Class|JobLogEvent|DTO for structured log events relevant to the UI: message, level, timestamp, job/table context, optional exception summary.|–|Task 60|
|ReportSyncer.Core.Observability|Class|JobCompletedEvent|DTO summarizing final outcome of a job, including `JobResult` and any warnings aggregated during execution.|`JobResult`|Task 40, 60|
|ReportSyncer.Core.Observability|Class|RunSummary|Persistent history model summarizing each job run (timestamp, job name, status, duration, rows deleted/inserted, dry-run flag). Deserialized directly for `getHistory` IPC calls.|`TableResult`|Task 60|
|ReportSyncer.Core.Observability|Class|TableResult|Per-table metrics: rows deleted, rows inserted, duration, warnings, effective filter range, identity insert usage. Used by `JobResult` and history.|–|Task 40, 50, 60|
|ReportSyncer.Core.Observability|Interface|IHistoryService|Abstraction for reading run history (and optionally appending summaries). Key methods: `Task<IReadOnlyList<RunSummary>> GetRecentRunsAsync(int count, CancellationToken ct)`, `Task AppendAsync(JobResult result, CancellationToken ct)`. Throws `SyncExecutionException` on IO failures.|`RunSummary`, `JobResult`|Task 60|
|ReportSyncer.Core.Observability|Class|LogHistoryService|Implementation of `IHistoryService` that reconstructs history from structured log files (Serilog JSON or equivalent). Knows filesystem layout but not UI concerns. Any parse error is logged and results are best-effort.|`IHistoryService`, `ILogService`|Task 60|
|ReportSyncer.Core.Observability|Class|WorkEstimator|Estimates total rows to be touched per table using `SELECT COUNT(*)` with filters. Key methods: `Task<WorkEstimate> EstimateAsync(TableExecutionContext ctx, CancellationToken ct)`. Used by dry-run, safety checks (`confirmLargeDeletePct`), and ETA calculations. Throws `SyncExecutionException` for DB failures.|`IDbContext`, `SqlServerQueryBuilder`, `ILogService`|Task 30, 40, 60|
|ReportSyncer.Core.Observability|Class|WorkEstimate|Value object with estimated rows to delete/insert and any warnings (e.g. “no primary key detected, count may be approximate”).|–|Task 60|
|ReportSyncer.Core.Observability|Class|ProgressTracker|Maintains EMA-based throughput and ETA over time. Key methods: `void OnBatchCompleted(string jobId, string tableName, long rowsProcessed)`, `JobProgressEvent GetSnapshot(...)`. Uses `RunConfig.etaSmoothing`. Pure math + state, no IO. Throws no domain exceptions.|`WorkEstimate`, `RunConfig`|Task 60|
|ReportSyncer.Core.Observability|Interface|ILogContextEnricher|Optional abstraction to add common metadata to logs (JobId, TableName, Phase). Used internally by logging integration so that `SyncOrchestrator` and `TableRunner` can keep logging calls simple.|`ILogService`|Task 60|
|ReportSyncer.Core.Observability|Class|LogContextEnricher|Default implementation of `ILogContextEnricher`, wraps `ILogService` or Serilog’s context. Ensures consistent structured logs for operations listed in the Minimal Doc test plan (JobStarting, PreFlightChecksPassed, etc.).|`ILogService`|Task 60|

---

#### `ReportSyncer.Core.Infrastructure`

|Namespace|Type|Name|Responsibility|Depends on|Related Tasks|
|---|---|---|---|---|---|
|ReportSyncer.Core.Infrastructure|Interface|INamedDbContextFactory|Creates `IDbContext` instances based on `ConnectionConfig` name. Key methods: `IDbContext Create(string connectionName)`, `IDbContext Create(ConnectionConfig connection)`. Centralizes mapping from config to provider-specific connection settings. Throws `ConfigurationException` if connection name is unknown.|`ConnectionConfig`, `DotNetToolkit.Database.Abstractions.IDbContext`, `DotNetToolkit.Database.Configuration.DatabaseSettings`|Task 20, 40, 50|
|ReportSyncer.Core.Infrastructure|Class|NamedDbContextFactory|Concrete `INamedDbContextFactory`. Uses `ConnectionConfig` list to build `DatabaseSettings` and configure `DotNetToolkit.Database` per connection (connection string + provider name). Ensures correct lifetime management for contexts. Throws `ConfigurationException` for missing / malformed connection entries.|`INamedDbContextFactory`, `IDbConnectionFactory`, `IDbContext`, `ILogService`|Task 20, 40, 50|
|ReportSyncer.Core.Infrastructure|Interface|IConnectionStringResolver|Optional helper abstraction that translates `ConnectionConfig` into `DatabaseSettings` used by toolkit. Keeps provider-specific concerns out of orchestrator/schema code. Key method: `DatabaseSettings Resolve(ConnectionConfig connection)`. Throws `ConfigurationException` on invalid config.|`ConnectionConfig`, `DatabaseSettings`|Task 20, 40|
|ReportSyncer.Core.Infrastructure|Class|ConnectionStringResolver|Concrete `IConnectionStringResolver` that reads provider type & conn string from `ConnectionConfig` (or defaults) and produces `DatabaseSettings` for `DotNetToolkit.Database`. No domain exceptions beyond `ConfigurationException`.|`IConnectionStringResolver`|Task 20, 40|
|ReportSyncer.Core.Infrastructure|Interface|IClock|Abstraction over system time for testability. Key methods: `DateTime UtcNow { get; }`. Used by orchestrator and history to stamp job start/end times.|–|Task 40, 60|
|ReportSyncer.Core.Infrastructure|Class|SystemClock|Default `IClock` implementation using `DateTime.UtcNow`. Very boring on purpose.|`IClock`|Task 40, 60|
|ReportSyncer.Core.Infrastructure|Interface|IRetryPolicy|Abstraction for DB retry behavior. Key methods: `Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct)` and generic versions. Used only around transient DB operations (schema inspection, count queries, DML).|–|Task 20, 50, 60|
|ReportSyncer.Core.Infrastructure|Class|ExponentialBackoffRetryPolicy|Concrete `IRetryPolicy` implementing simple exponential backoff with configurable max attempts. Translates non-transient exceptions straight through. Throws `SyncExecutionException` after exhausting retries.|`IRetryPolicy`, `ILogService`|Task 20, 50, 60|
|ReportSyncer.Core.Infrastructure|Interface|ICancellationTokenSourceProvider|Optional abstraction around cancellation tokens for jobs, so injection/testing is easier. Key method: `CancellationTokenSource CreateJobScopeTokenSource()`.|–|Task 40|
|ReportSyncer.Core.Infrastructure|Class|CancellationTokenSourceProvider|Default implementation of `ICancellationTokenSourceProvider` using `new CancellationTokenSource()`. Host can still wrap externally if needed.|`ICancellationTokenSourceProvider`|Task 40|

---

### Coverage sanity check

- **Config & safety rules** are clearly owned by `ConfigurationValidator`, `SafetyValidator`, and the configuration model classes. (All Task 10 behaviors tied to a real class.)
    
- **Schema inspection, mapping, FK ordering** have a clean cluster: `ISchemaInspector` / `SqlServerSchemaInspector`, `ISchemaMapper` / `SchemaMapper`, `IDependencyResolver` / `SqlServerDependencyResolver`.
    
- **Permissions & safety guardrails** are all in `Security`, not randomly glued into the orchestrator.
    
- **Execution pipeline** is strictly controlled: external callers only talk to `ISyncOrchestrator`; all mutation flows through `TableRunner` + `IDataWriter`.
    
- **Data movement behavior** (chunked delete, batch insert, TVP, identity insert, filter injection, dedupe) is owned by `SqlDataWriter`, `SqlServerQueryBuilder`, `IdentityInsertManager`.
    
- **Logging, history & progress** are centralized in `Observability` and abstracted via `IJobProgressReporter` / `IHistoryService` rather than ad-hoc logs sprinkled everywhere.
    
- **Toolkits** are cleanly depended on via `INamedDbContextFactory` + `DotNetToolkit.Database` / `DotNetToolkit.Logging`, with no domain logic leaking into them.
    

If you see anything still smelling like “god object,” it’s `SyncOrchestrator` and `PreFlightValidator`, but their responsibilities are sharply bounded: orchestrate vs pre-check. If we try to split them further at this stage, we’ll just create ceremony without real benefit.


## Error Model & Invariants

This section defines:

- The **exception taxonomy** used in `ReportSyncer.Core`
    
- How each major operation **fails**, what it **throws**, and how the host reacts
    
- Global **invariants** that must never be violated
    

The goal is that coding agents never guess what to throw, where to catch, or how to log.

---

#### A4.1 Exception taxonomy

**Core domain exceptions (authoritative list for backend)**

|Exception|Semantics|Typical throwers|Retryable?|Who catches first|
|---|---|---|---|---|
|`ConfigurationException`|Invalid or inconsistent configuration (YAML parse, missing fields, impossible combinations, bad CLI args once mapped to config). No DB was touched.|`IConfigurationLoader`, `ConfigurationValidator`, `INamedDbContextFactory`, CLI argument binder.|No. Fix config / args then rerun.|`ReportSyncer.Console` top-level; Web API controller if added later.|
|`SchemaMismatchException`|Source/target schema incompatibility: missing tables/columns, incompatible types under current `SchemaPolicyConfig`, FK cycles that break execution plan.|`ISchemaInspector`, `ISchemaMapper`, `IDependencyResolver`, `SchemaPolicyEvaluator`.|No. Requires schema/config change.|Top-level host; optionally intercepted by `PreFlightValidator` to aggregate messages before rethrow.|
|`SafetyViolationException`|Guardrail triggered: same-connection block, prod→prod disallowed, unbounded delete with `allowAllDelete=false`, large delete above threshold, missing filter where policy requires one.|`SafetyValidator`, possibly `PreFlightValidator` when combining stats + config.|No. User must acknowledge/adjust config / safety flags.|Top-level host. Used to render “blocked for safety” messaging.|
|`SyncExecutionException`|Operational runtime failure **after** config & pre-flight passed: DB connectivity issues, SQL timeouts, constraint violations, TVP misuse, identity insert failures, IO failures when writing history, etc.|`SqlServerSchemaInspector`, `SqlDataWriter`, `IdentityInsertManager`, `WorkEstimator`, `IHistoryService` implementations.|Sometimes. Treated as non-retryable by default; only low-level components can opt into retry via `IRetryPolicy`.|Top-level host for final message; mid-level services generally log and rethrow.|
|`UserCancelledException`|User explicitly cancelled the operation (Ctrl+C, UI cancel). Used to differentiate between “failed” and “cancelled by user”.|`SyncOrchestrator` or host adapter mapping from `OperationCanceledException`.|No. This is a normal termination path.|Top-level host maps to “cancelled” exit code / HTTP 499-like status.|
|`UnexpectedInternalException` (optional)|Catch-all wrapper only at the **very outer edge** when something leaks that we didn’t anticipate (null ref, bug). Used to avoid showing raw stack traces to user while still logging full details.|Top-level host only, wrapping unknown exceptions.|No.|Never thrown from core; only the host wraps unknown exceptions into this for user-facing messages.|

**Mapping from low-level to domain exceptions**

- YAML / file IO errors → `ConfigurationException` (never leak `IOException` to caller).
    
- SQL “object not found”, missing table/column → `SchemaMismatchException`.
    
- SQL constraint violations, deadlocks, timeouts during write/estimate → `SyncExecutionException`.
    
- Any safety rule failure from `SafetyConfig` or `SafetyValidator` → `SafetyViolationException`.
    
- `OperationCanceledException` during long-running operations:
    
    - If initiated by user cancellation → translated to `UserCancelledException` at orchestrator boundary.
        
    - If internal misuse → treated as `SyncExecutionException` (bug / misuse).
        

---

#### A4.2 Failure model by operation

This table describes how each major operation fails and how the system is expected to behave.

|Operation|Typical failure condition|Exception thrown|Behavior in Core|Behavior in host (`ReportSyncer.Console`)|
|---|---|---|---|---|
|Load configuration (`IConfigurationLoader.LoadAsync`)|File not found, YAML parse error, invalid enum values, missing mandatory sections.|`ConfigurationException`|Log at `Error`, no retries. Pre-flight stops immediately.|Print concise message pointing to config file & section, exit with **code 2**.|
|Validate configuration (`ConfigurationValidator.Validate`)|Conflicting flags (e.g. `allowAllDelete=false` but `preSyncTargetAction=Truncate`), unknown connection, invalid thresholds.|`ConfigurationException` or `SafetyViolationException`|Fail fast before any DB access. No retries.|Log reason; for `ConfigurationException` exit **2**, for `SafetyViolationException` exit **4** with “blocked by safety rules”.|
|Create DB context (`INamedDbContextFactory.Create`)|Unknown connection name, unsupported provider, invalid connection string.|`ConfigurationException`|No retry; considered design/config error.|Exit **2**.|
|Inspect schema (`ISchemaInspector.GetTableSchemaAsync`)|Table not found, column not found, insufficient permissions to read metadata, connectivity errors.|`SchemaMismatchException` (missing objects) or `SyncExecutionException` (permissions/connectivity).|May be wrapped in `PreFlightValidator` to aggregate multiple errors then rethrow first / combined message.|Schema mismatch → exit **3**; execution error → exit **5**.|
|Build execution plan (`IDependencyResolver.BuildExecutionPlan`)|FK cycles, referenced tables not included in job, inconsistent FK metadata.|`SchemaMismatchException`|No retry; considered design or schema issue.|Exit **3**.|
|Map schemas (`ISchemaMapper.CompareAndMap`)|Incompatible types beyond allowed coercion policy; required columns missing; unsupported mapping config.|`SchemaMismatchException`|No retry; pre-flight fails and core must not proceed to data mutation.|Exit **3**.|
|Permissions profiling (`IPermissionProfiler.ProbeTablePermissionsAsync`)|Lack of delete/insert permission; inability to set `IDENTITY_INSERT`; probe statement blocked by DB policy.|`SyncExecutionException` or `SafetyViolationException` (if mapped directly into rule violation).|If exception indicates “unsafe to proceed” → `SafetyViolationException`; otherwise treat as execution error and abort job.|Safety violation → exit **4**; execution problem → exit **5**.|
|Safety checks (`SafetyValidator`)|Same connection for source and target when disallowed, prod→prod copy forbidden, estimated delete exceeds threshold without override.|`SafetyViolationException`|Never retried. Pre-flight fails and no mutation is allowed.|Exit **4** with clear “blocked” messaging.|
|Work estimation (`WorkEstimator.EstimateAsync`)|Timeout/lock on `SELECT COUNT(*)`, connectivity failure.|`SyncExecutionException`|May use `IRetryPolicy` for transient failures. If still failing, pre-flight fails (no partial run).|Exit **5**.|
|Run delete phase (`TableRunner`/`SqlDataWriter.DeleteAsync`)|SQL errors during delete, deadlocks, constraints, permission failures, malformed WHERE.|`SyncExecutionException`|May use `IRetryPolicy` for transient DB errors. If unrecoverable, abort job and mark as failed; per-table `TableResult` indicates failure.|Exit **5**.|
|Run insert phase (`TableRunner`/`SqlDataWriter.InsertAsync`)|Constraint violations, TVP usage error, identity insert not allowed, data conversion failures.|`SyncExecutionException`|No automatic retry for logical failures (constraint violations, conversions). For clearly transient (e.g. deadlock), retry via `IRetryPolicy`. Job marked failed.|Exit **5**.|
|Identity insert handling (`IdentityInsertManager`)|Failed to set `IDENTITY_INSERT ON/OFF`, connection lost during scope.|`SyncExecutionException`|Always attempts `OFF` in `finally`. If that fails, log at `Fatal` and propagate exception.|Exit **5**; message must clearly mention identity insert failure.|
|History append (`IHistoryService.AppendAsync`)|Log file IO/parse issues, path permission problems.|`SyncExecutionException`|**Must not** hide job result. Job result is still returned as completed; history write failure is logged separately and may raise exception only if spec says history is required.|If append failure is considered non-fatal: log `Warning` but keep exit code based on job outcome. If considered fatal: exit **5** with explicit “history write failure” note.|
|Cancellation (`SyncOrchestrator` and down-stack)|User cancellation token triggered while pre-flight or execution is running.|`UserCancelledException` at orchestrator boundary (or raw `OperationCanceledException` internally)|All components must honor `CancellationToken`; no further DML executed after cancellation observed.|Exit **6**, message “Operation cancelled by user.”|
|Unexpected bug / unhandled exception|Null reference, index out of range, etc., that escape core.|Raw exception at core boundary, wrapped as `UnexpectedInternalException` only in host.|No retry. This is a bug.|Log full stack at `Fatal`, show generic “unexpected internal error” to user, exit **99**.|

---

#### A4.3 Logging & exit code mapping

**Log level guidelines per exception:**

- `ConfigurationException`
    
    - **Level:** `Error`
        
    - Context: config file path, section, key. No stack trace needed for normal user output; full stack in debug logs.
        
- `SchemaMismatchException`
    
    - **Level:** `Error`
        
    - Context: table, column, policy (`onMismatch`), expected vs actual types.
        
- `SafetyViolationException`
    
    - **Level:** `Warning` for rejected operations, `Error` if thrown mid-run due to dynamic information.
        
    - Context: which rule fired, estimated rows, relevant flags (`allowAllDelete`, `confirmLargeDeletePct`).
        
- `SyncExecutionException`
    
    - **Level:** `Error`; `Fatal` if it leaves DB in uncertain state (e.g. failure to turn `IDENTITY_INSERT OFF`).
        
    - Context: connection name, table, command type (DELETE/INSERT/COUNT), SQL state / error number if available.
        
- `UserCancelledException`
    
    - **Level:** `Information` (user chose to cancel).
        
    - Context: job name, elapsed time, rows processed so far.
        
- `UnexpectedInternalException`
    
    - **Level:** `Fatal`
        
    - Context: full stack trace, correlation ID, config hash, etc.
        

**Standard exit codes for `ReportSyncer.Console`**

|Exit code|Meaning|Primary sources|
|---|---|---|
|0|Success|Normal completion of `ISyncOrchestrator` with no fatal errors.|
|1|CLI usage error|Invalid CLI args before configuration is even built. Argument parsing only.|
|2|Configuration error|`ConfigurationException` from loader/validator/factory.|
|3|Schema mismatch|`SchemaMismatchException`.|
|4|Safety violation (blocked)|`SafetyViolationException`.|
|5|Sync execution failure|`SyncExecutionException`.|
|6|User cancelled|`UserCancelledException` / `OperationCanceledException` mapped by host.|
|99|Unexpected internal error|Anything else wrapped into `UnexpectedInternalException` at host level.|

Hosts other than `ReportSyncer.Console` (e.g. Web API) map the same exceptions to their own error contract (HTTP codes / error codes) but **do not invent new exception types**.

---

#### A4.4 Invariants

These are **non-negotiable rules**. Violating any of them is a bug.

1. **Pre-flight gate**
    
    - `SyncOrchestrator` must **never** start delete/insert operations if `PreFlightValidator` fails or throws.
        
    - If pre-flight fails with any domain exception, the job status is `Failed_PreFlight` and no DML reaches the target DB.
        
2. **Validated config only**
    
    - No DB operation (including schema inspections and permission probes) is executed using an unvalidated `SyncConfiguration`.
        
    - `ISyncOrchestrator` always uses `IConfigurationProvider.LoadAndValidateAsync` instead of talking directly to `IConfigurationLoader`.
        
3. **Dry-run purity**
    
    - When `RunConfig.dryRun == true`, **no** DML statements (`INSERT`, `DELETE`, `UPDATE`, `MERGE`, `TRUNCATE`) are executed against the target.
        
    - Schema inspection, permission probes, and `SELECT COUNT(*)` are allowed; they must be clearly marked as read-only.
        
4. **Identity insert balance**
    - Any `SET IDENTITY_INSERT <table> ON` must be paired with a successful `SET IDENTITY_INSERT <table> OFF` in all success and error paths.
    - If `OFF` fails, `IdentityInsertManager` logs at `Fatal` and throws `SyncExecutionException`. Host must treat this as a hard failure.
        
5. **No unbounded destructive operations by default**
    
    - The system must never execute an unfiltered `DELETE` or any `TRUNCATE` on a target table **unless**:
        
        - The relevant `TableTaskConfig` explicitly allows it (`allowAllDelete == true` or equivalent), **and**
            
        - Safety rules (like `confirmLargeDeletePct`) are satisfied.
            
    - Any attempt to construct such a command without those preconditions must fail with `SafetyViolationException` during pre-flight.
        
6. **Deterministic error propagation**
    
    - Core services (`PreFlightValidator`, `TableRunner`, `SqlDataWriter`, etc.):
        
        - May **translate** low-level exceptions to domain exceptions.
            
        - Must **not** swallow domain exceptions. Any `ConfigurationException`, `SchemaMismatchException`, `SafetyViolationException`, or `SyncExecutionException` that reaches them must either be rethrown or wrapped with more context, never silently handled.
            
7. **Idempotent error reporting**
    - When a table run fails, `TableRunner` must:
        - Record a `TableResult` with `Status=Failed` and relevant error info.
        - Ensure that `JobResult` reflects partial failure even if history logging later fails.
            
8. **Cancellation is cooperative**
    - All long-running loops (chunked deletes, batched inserts, count estimations) must check the `CancellationToken` and stop **before** starting a new batch if cancellation is requested.
    - Cancellation must not leave partially configured global DB state (e.g. `IDENTITY_INSERT ON` with no attempt to turn it off).
        
9. **No host-specific logic in core**
    
    - Core must never inspect `Environment.ExitCode`, CLI args, or UI-specific constructs.
        
    - Host-specific reactions (exit codes, HTTP statuses, UX messages) are driven purely by which **domain exception** is observed.
        
10. **Consistent classification**
	- The same failure condition, regardless of where it is observed, maps to the **same** domain exception:
	    - Schema incompatibility → `SchemaMismatchException`.
	    - Safety rule violation → `SafetyViolationException`.
	    - Operational DB/IO error → `SyncExecutionException`.
	    - Bad config / usage → `ConfigurationException`


---

## Test Surfaces & Seams (Task A5)

This section defines **where** tests attach and **what type** they should be:

- Which classes are **pure unit-test** targets.
- Which must be tested via **DB-backed integration tests**.
- Which orchestration flows require **end-to-end style** tests.
- A **test matrix skeleton** with example test names.
    
Assumed test project: `ReportSyncer.Tests`, with logical grouping into `Unit` and `Integration` (folder/namespace, not necessarily separate projects).

---

#### A5.1 Classification rules

To avoid random “integration tests for everything”:

- **Unit tests** target:
    - Classes with no real I/O: pure logic, mapping, validation, safety rules, orchestration that depends only on injected interfaces.
    - Use mocks/fakes for:
        - `IDbContext`
        - `IJobProgressReporter`
        - `IHistoryService`
        - `INamedDbContextFactory`
        - `ILogService`
            
- **Integration tests** target:
    - Anything that talks to a **real SQL Server** or filesystem.
    - Any class that builds and executes SQL commands or touches real configuration files.
        
- **Hybrid / scenario tests**:
    - `SyncOrchestrator`, `PreFlightValidator`, and `TableRunner` get:
        - Narrow unit tests (with mocks) for edge cases.
        - Scenario-style integration tests that execute against LocalDB / seeded database.
            

---

#### A5.2 Test surfaces by namespace

##### Configuration (`ReportSyncer.Core.Configuration`)

|Component|Test Type|Notes / Seams|
|---|---|---|
|`SyncConfiguration` & sub-config classes (`RunConfig`, `SchemaPolicyConfig`, `SafetyConfig`, `ConnectionConfig`, `SyncJobConfig`, `TableTaskConfig`, etc.)|Unit|Pure models. Tested for validation rules, defaulting behavior, and invariants. No I/O.|
|`IConfigurationLoader` / `YamlConfigurationLoader`|Both|Unit: YAML shapes → object model using in-memory strings or embedded test files. Integration: actual YAML files on disk under `TestData/Config`. Failures must raise `ConfigurationException`.|
|`IConfigurationValidator` / `ConfigurationValidator`|Unit|Given `SyncConfiguration` instances, assert correct detection of invalid combinations and correct exception type (`ConfigurationException` vs `SafetyViolationException`).|
|`IConfigurationProvider`|Unit|Test orchestration only: loader called once, validator called once, exceptions propagated unchanged.|

**Unit focus:** validation rules, parameter expansion, mapping from YAML to `SyncConfiguration`.  
**Integration focus:** “Real” config load using actual file paths and CLI-style scenarios.

---

##### Schema (`ReportSyncer.Core.Schema`)

|Component|Test Type|Notes / Seams|
|---|---|---|
|`TableSchema`, `ColumnSchema`, `ForeignKeyRelation`, `ExecutionPlan`, `SchemaMappingResult`|Unit|Simple models. Tests only assert correct construction and immutability semantics.|
|`ISchemaMapper` / `SchemaMapper`|Unit|Given fake `TableSchema` objects, check compatible vs incompatible mappings, added columns, automap, type coercion behavior. No DB required.|
|`ISchemaPolicyEvaluator` / `SchemaPolicyEvaluator`|Unit|Given mismatched types & policies, assert when `SchemaMismatchException` vs warnings.|
|`IDependencyResolver` / `SqlServerDependencyResolver`|Unit|Operate on in-memory `ForeignKeyRelation` sets. Test valid DAG ordering and cycle detection.|
|`ISchemaInspector` / `SqlServerSchemaInspector`|Integration|Requires real SQL Server (LocalDB/Testcontainers) with seeded schema. Tests query system catalog and verify `TableSchema` / `ForeignKeyRelation` are correctly constructed.|

**Unit focus:** mapping logic, FK ordering, policy application.  
**Integration focus:** schema inspection via real system tables.

---

##### Security (`ReportSyncer.Core.Security`)

|Component|Test Type|Notes / Seams|
|---|---|---|
|`PermissionsProfile`, `EstimatedDeleteStats`, `SyncJobContext`|Unit|Value objects; targeted in tests as DTOs and inputs for validators.|
|`ISafetyValidator` / `SafetyValidator`|Unit|Given configurations + `PermissionsProfile` + `EstimatedDeleteStats`, verify each rule fires the expected `SafetyViolationException` or passes.|
|`IEnvironmentClassifier` / `NameBasedEnvironmentClassifier`|Unit|Use synthetic `ConnectionConfig` instances; verify prod/dev classification from tags/naming.|
|`IPermissionProfiler` / `SqlServerPermissionProfiler`|Integration|Requires DB configured with varying permissions (at least: full rights, no delete, no identity insert). Tests must assert: correct detection of `CanDelete`, `CanInsert`, identity insert capability, and proper mapping to `SyncExecutionException` when probes fail.|

**Unit focus:** pure safety and classification rules.  
**Integration focus:** real permission probing semantics.

---

##### Sync (`ReportSyncer.Core.Sync`)

|Component|Test Type|Notes / Seams|
|---|---|---|
|`ISyncOrchestrator` / `SyncOrchestrator`|Both|Unit: use mocks for all dependencies (config provider, preflight, table runner, reporter, history) to verify lifecycle (preflight before execution, correct status mapping, cancellation behavior). Integration: run small jobs end-to-end against LocalDB.|
|`PreFlightValidator` / `PreFlightResult`|Both|Unit: given mocks for schema inspector / mapper / dependency resolver / permission profiler / safety validator, assert correct calls and failure short-circuiting. Integration: combine real DB + real config to validate preflight behavior across tables.|
|`ITableRunner` / `TableRunner`|Both|Unit: using a fake `IDataWriter` and `IdentityInsertManager`, verify order: delete → insert, identity scope usage, reaction to writer failures. Integration: execute per-table syncs against LocalDB.|
|`IDataWriter` / `SqlDataWriter`|Integration|Must be validated against a real DB for DELETE chunking, INSERT batching, TVP usage, handling of NULL/identity/business keys.|
|`IdentityInsertManager` / `IdentityInsertScope`|Integration|Requires a table with identity column. Tests assert `SET IDENTITY_INSERT` ON/OFF behavior, including failure and exception paths.|
|`SqlServerQueryBuilder`|Unit|Pure SQL string builder. Tests assert generated SQL text & parameters for typical scenarios and edge cases.|
|`DeleteCommandContext`, `InsertCommandContext`, `TableExecutionContext`, `JobResult`, `TableResult`|Unit|Value object behavior, mapping of inputs to properties, aggregation of per-table results.|

**Unit focus:** orchestration/control flow, SQL generation, result aggregation.  
**Integration focus:** DML behavior (delete/insert, identity insert, performance patterns) against real DB.

---

##### Observability (`ReportSyncer.Core.Observability`)

|Component|Test Type|Notes / Seams|
|---|---|---|
|`ProgressTracker`|Unit|Pure math/state: throughput & ETA based on `RunConfig.etaSmoothing`. Tests feed synthetic batch completions and assert ETAs/metrics.|
|`WorkEstimator` / `WorkEstimate`|Integration|Uses `SELECT COUNT(*)` with filters. Tests use seeded DB and verify counts & warnings.|
|`IJobProgressReporter` and DTOs (`JobProgressEvent`, `JobLogEvent`, `JobCompletedEvent`, `RunSummary`)|Unit|Events treated as simple DTOs; tests ensure they carry correct data and serialization shape if needed.|
|`IHistoryService` / `LogHistoryService`|Both|Unit: with fake log source, assert filtering/aggregation. Integration: run full jobs and assert history readout from real log files / storage.|
|`ILogContextEnricher` / `LogContextEnricher`|Unit|Using fake `ILogService`, assert that context (JobId, TableName, Phase) is added correctly.|

---

##### Infrastructure (`ReportSyncer.Core.Infrastructure`)

|Component|Test Type|Notes / Seams|
|---|---|---|
|`INamedDbContextFactory` / `NamedDbContextFactory`|Both|Unit: invalid config → `ConfigurationException`. Integration: given real connection strings, can open `IDbContext` and run a trivial query.|
|`IConnectionStringResolver` / `ConnectionStringResolver`|Unit|Transformation from `ConnectionConfig` → `DatabaseSettings`. Tests verify correct provider name and conn string.|
|`IClock` / `SystemClock`|Unit|Trivial, but used via fakes for deterministic timestamp assertions.|
|`IRetryPolicy` / `ExponentialBackoffRetryPolicy`|Unit|Use fake time or inject backoff sequence; test retry count, retry on transient vs non-transient, and final exception behavior.|
|`ICancellationTokenSourceProvider` / `CancellationTokenSourceProvider`|Unit|Generally trivial; mostly used indirectly via orchestrator tests.|

---

#### A5.3 DB test environment expectations

For DB-touching tests (`SqlServerSchemaInspector`, `SqlServerPermissionProfiler`, `SqlDataWriter`, `IdentityInsertManager`, `WorkEstimator`, scenario tests):

- Use **SQL Server LocalDB** or a **containerized SQL Server** instance with:
    
    - Seeded schema that mimics:
        
        - Dimension table with identity PK.
            
        - Fact-like table with FK to dimension.
            
        - A few extra tables for FK and permission variants.
            
    - Seeded data for:
        
        - Basic sync scenarios (filtered by `CustomerId`).
            
        - Large-ish tables for delete chunking tests.
            
- Use **test schema** (`rs_test` or similar) to avoid impacting real environments.
    
- Schema/data must be initialized once per test suite (fixture-level) to avoid test flakiness and keep runs fast.
    

**Rules:**

- Unit tests must run with **no DB dependency** and be fast (<100 ms each).
    
- Integration tests can be slower but must be deterministic:
    
    - Explicit setup/teardown per test or per fixture.
        
    - No date-time sensitive logic without controlling `IClock`.
        

---

#### A5.4 Test matrix skeleton

Below is a **minimal matrix**: categories, target components, and example test names. It’s not every test, just enough structure so test agents don’t improvise garbage.

##### Configuration

- **Unit**
    
    - `ConfigurationValidator_UnknownConnection_ThrowsConfigurationException`
        
    - `YamlConfigurationLoader_ValidYaml_LoadsExpectedSyncConfiguration`
        
    - `YamlConfigurationLoader_MissingRequiredField_ThrowsConfigurationException`
        
    - `ConfigurationValidator_PreSyncWithoutAllowAllDelete_ThrowsSafetyViolation`
        

##### Schema

- **Unit**
    
    - `SchemaMapper_CompatibleTypesWithAutomap_ProducesExpectedMapping`
        
    - `SchemaMapper_IncompatibleTypesAndPolicyFail_ThrowsSchemaMismatchException`
        
    - `DependencyResolver_SimpleHierarchy_ReturnsCorrectInsertAndDeleteOrder`
        
    - `DependencyResolver_CycleDetected_ThrowsSchemaMismatchException`
        
- **Integration**
    
    - `SqlServerSchemaInspector_TableWithIdentity_ExposesIdentityFlagCorrectly`
        
    - `SqlServerSchemaInspector_TableWithForeignKey_ResolvesForeignKeyRelations`
        

##### Security

- **Unit**
    
    - `SafetyValidator_SameConnectionForbidden_ThrowsSafetyViolation`
        
    - `SafetyValidator_DeleteAboveThresholdWithoutOverride_ThrowsSafetyViolation`
        
    - `NameBasedEnvironmentClassifier_ProdSuffix_ClassifiesAsProd`
        
- **Integration**
    
    - `SqlServerPermissionProfiler_NoDeletePermission_CanDeleteIsFalse`
        
    - `SqlServerPermissionProfiler_NoIdentityInsertPermission_FlagsIdentityRestriction`
        

##### Sync

- **Unit**
    
    - `SyncOrchestrator_PreFlightFails_DoesNotInvokeTableRunner`
        
    - `SyncOrchestrator_CancellationRequested_ReportsCancelledJobResult`
        
    - `TableRunner_DeleteThenInsert_OrderIsCorrect`
        
    - `SqlServerQueryBuilder_DeleteWithFilter_GeneratesExpectedSql`
        
- **Integration (single-table / small job)**
    
    - `SyncOrchestrator_FullDeleteThenInsert_SyncsTargetToSource`
        
    - `SyncOrchestrator_AppendOnlySync_DoesNotDeleteExistingRows`
        
    - `TableRunner_WithIdentityInsert_InsertsWithExplicitIdentityValues`
        
    - `SqlDataWriter_DeleteChunking_DeletesAllRowsInChunks`
        
- **E2E-style**
    
    - `EndToEnd_MultiTableJobWithFk_RespectsDependencyOrder`
        
    - `EndToEnd_DryRun_DoesNotMutateTargetButProducesEstimates`
        
    - `EndToEnd_CancelDuringInsert_JobStatusIsCancelledAndIdentityInsertOff`
        

##### Observability & History

- **Unit**
    
    - `ProgressTracker_MultipleBatches_ComputesEtaAndThroughput`
        
    - `ProgressTracker_ZeroWork_DoesNotCrashAndReportsZeroTotal`
        
- **Integration**
    
    - `WorkEstimator_FilteredCount_MatchesExpectedRowCount`
        
    - `HistoryService_AfterSuccessfulRun_ReturnsRunSummaryWithCorrectMetrics`
        

##### Infrastructure

- **Unit**
    
    - `NamedDbContextFactory_UnknownConnection_ThrowsConfigurationException`
        
    - `ConnectionStringResolver_ValidConnection_ProducesExpectedDatabaseSettings`
        
    - `ExponentialBackoffRetryPolicy_TransientFailure_RetriesConfiguredTimes`
        
- **Integration**
    
    - `NamedDbContextFactory_WithValidConnection_CanOpenAndQueryDatabase`
        

---

This makes the test story **explicit**:

- Pure logic classes are **unit-only** and must not drag a database into the picture.
    
- DB-facing classes are **integration-only** and must not be mocked into oblivion.
    
- Orchestrators and runners get **both**: tight unit tests for control flow, plus scenario tests to prove the whole thing actually moves data the way it claims.**