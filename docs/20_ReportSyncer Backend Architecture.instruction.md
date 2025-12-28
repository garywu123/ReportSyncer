
## 1. Technology Stack & Constraints

All backend development must adhere to the following technology choices. No unauthorized libraries are permitted.

| **Category**             | **Choice**                                                     | **Notes**                                                                                                                                                                                                                                             |
| ------------------------ | -------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Framework**            | **.NET 9**                                                     | Target `net9.0` for all projects.                                                                                                                                                                                                                     |
| **Language**             | C# 12/13                                                       | Use modern features (records, pattern matching) where appropriate.                                                                                                                                                                                    |
| **Dependency Injection** | `Microsoft.Extensions.DependencyInjection`                     | Abstractions only in Core. `DryIoc` allowed in Host composition roots only.                                                                                                                                                                           |
| **Logging**              | `Serilog`                                                      | `ReportSyncer.Core` depends on `DotNetToolkit.Logging.ILogService`. Hosts configure Serilog sinks (Console, File, IPC).                                                                                                                               |
| **Database Access**      | `DotNetToolkit.Database`                                       | **NO EF Core.** Use provided toolkit abstractions (`IDbContext`, `IDbCommandWrapper`) + raw SQL for bulk ops.                                                                                                                                         |
| **Configuration**        | `YamlDotNet`                                                   | Used in `ReportSyncer.Core.Configuration` to parse YAML.                                                                                                                                                                                              |
| **Serialization**        | `Newtonsoft.Json`                                              | Standard for IPC messages and history files.                                                                                                                                                                                                          |
| **Testing**              | `xUnit`, `FluentAssertions`, `Moq`, `DotNetToolkit.TestHelper` | Unit tests for logic; Integration tests (LocalDB) for data access. You should also use `DotNetToolkit.TestHelper` to generalize some common testing tool like a database setup. Dependency Injection, Common Fake Classes like Fake Logger and so on. |

## 2. Project Layout & Responsibilities

The solution uses a **layered, domain-centric** architecture.
### 2.1 Project List


| **Project**                  | **Type**  | **Category** | **Responsibility**                                                                                                                              |
| ---------------------------- | --------- | ------------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| **`ReportSyncer.Core`**      | Class Lib | **Domain**   | **The Brain.** Contains all business logic: config parsing, schema inspection, safety validation, sync orchestration, and SQL generation logic. |
| **`ReportSyncer.Console`**   | EXE       | **Host**     | **The Runner.** CLI host for batch/ops. Bootstraps DI, loads config, wires logging, and calls `ISyncOrchestrator`.                              |
| **`ReportSyncer.WebApi`**    | EXE       | **Host**     | **The API.** (Future) HTTP host for Electron/React. Exposes endpoints for UI to control the Core. Handles Auth/DTO mapping only.                |
| `ReportSyncer.Wpf`           | EXE       | **Host**     | **The Desktop UI.** 基于 WPF (MVVM) 构建。直接引用 Core，负责任务可视化、参数输入、连接管理和历史展示。                                                                          |
| `ReportSyncer.Core.Tests`    | Test Proj | **Test**     | Unit and Integration tests for `ReportSyncer.Core` project. Contains fixtures for LocalDB and seeded data.                                      |
| `ReportSyncer.Wpf.Tests`     | Test Proj | **Test**     | Unit and Integration tests for `ReportSyncer.Wpf` project.                                                                                      |
| **`DotNetToolkit.General`**  | Class Lib | **Shared**   | Domain-agnostic utilities (Guard clauses, functional extensions).                                                                               |
| **`DotNetToolkit.Logging`**  | Class Lib | **Shared**   | Logging abstractions (`ILogService`) and generic adapters.                                                                                      |
| **`DotNetToolkit.Database`** | Class Lib | **Shared**   | Low-level DB helpers (`IDbContext`, connection factories, retry policies).                                                                      |

### 2.2 Dependency Rules

**Strict Layering Enforcement:**

```
ALLOWED References:
  ReportSyncer.Core     -> DotNetToolkit.*
  ReportSyncer.Console  -> ReportSyncer.Core, DotNetToolkit.*
  ReportSyncer.WebApi   -> ReportSyncer.Core, DotNetToolkit.*
  ReportSyncer.Tests    -> ReportSyncer.Core, ReportSyncer.Console, DotNetToolkit.*
  ReportSyncer.Wpf -> ReportSyncer.Core, DotNetToolkit.* 
  ReportSyncer.Core -> DotNetToolkit.*

FORBIDDEN References:
  DotNetToolkit.* -> ReportSyncer.* (Toolkits must never know about the Domain)
  ReportSyncer.Core     -> ReportSyncer.Console (Domain never calls Host)
  ReportSyncer.Core     -> ReportSyncer.WebApi (Domain never calls Host)
  ReportSyncer.Console  -> ReportSyncer.WebApi (Hosts don't talk to each other)
```

## 3. Architecture Style & Core Patterns

### 3.1 Orchestration Pipeline

The system follows a strict **Pipeline Pattern** managed by `ISyncOrchestrator`:
1. **Load:** Host loads/validates `SyncConfiguration` (YAML) via `IConfigurationLoader`.
2. **Pre-Flight:** `ISyncOrchestrator` calls `PreFlightValidator`.
    
    - _Checks:_ Connection Liveness -> Schema Inspection -> Dependency Resolution -> Permission Profiling -> Safety Validation.
        
    - _Result:_ If any check fails, the pipeline aborts. **No data mutation allowed.**
        
3. **Plan:** `IDependencyResolver` builds the execution order (DAG sort).
    - Delete Order: children → parents.
    - Insert Order: parents → children.
    - The resolver operates on the **subset of tables participating in the job** (as defined by the `TableTasks` in `SyncConfiguration`).
    - As part of planning, dependency resolution must validate that **every selected table's foreign key dependencies are satisfied within this subset**.
        - If a selected table has a foreign key to another Target table that is **not present** in the job's table list, the resolver must surface a fatal pre-flight error (e.g., via `SchemaMismatchException`) listing the missing tables and their dependents.
        - Add an exception note to the "Missing parent table leads to failure" rule: "Unless the corresponding `TableTask` is configured with `ignoreDependencies: true`. In this case, the planner will disconnect the logical edge between this table and the unselected parent table."
        - Hosts must not attempt to "fix" this by auto-adding tables or silently dropping dependent tables; the job is considered invalid until the configuration is corrected.

4. **Execute:** `ISyncOrchestrator` iterates through `TableTasks`.
    
    - Invokes `ITableRunner` for each table.
        
    - `ITableRunner` manages `IDataWriter` (SQL generation, Identity Insert scoping, chunking).
        

### 3.2 Host Responsibility

Hosts (`Console`, `WebApi`) are **"Humble Shells"**. They must NOT contain business rules.

- **DO:** Parse args, wire DI, catch top-level exceptions, exit with codes.
    
- **DON'T:** Decide if a sync is safe, write SQL, or parse YAML manually.
    

## 4. Class & Interface Inventory

### 4.1 `ReportSyncer.Core.Configuration`

_Responsibilities: Loading, Parsing, and Validating the YAML definitions._

| **Type**    | **Name**                      | **Responsibility**                                                                                                                                                                                                        |
| ----------- | ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `class`     | **`SyncConfiguration`**       | Root config model. Contains `connections`, `syncJobs`, `run`, `safety`, `schemaPolicy`.                                                                                                                                   |
| `class`     | **`ConnectionConfig`**        | Defines a DB connection. **Crucial:** Properties include `Name`, `ConnectionString`, `Environment` (Prod/Dev), and **`Type` (Application/Reporting)** for context logic.                                                  |
| `class`     | **`TableTaskConfig`**         | Config for a single table sync. Includes `source`, `target`, `preSyncTargetAction`, `allowAllDelete`, `filter`. `ignoreDependencies`, `ColumnMapping`                                                                     |
| `class`     | **`FilterConfig`**            | Represents optional table filter. Can define a key-based scope (`keyColumn`/`value`), a date range (`dateColumn`/`startDate`/`endDate`), or **both** (combined with `AND` logic). Used by SQL builder and safety logic.\| |
| `interface` | **`IConfigurationLoader`**    | Contract for loading config from disk/stream.                                                                                                                                                                             |
| `class`     | **`YamlConfigurationLoader`** | Implementation using `YamlDotNet`. Handles file I/O and deserialization errors.                                                                                                                                           |
| `class`     | **`ConfigurationValidator`**  | Enforces static rules (e.g., unique names, required fields, basic safety flags). Throws `ConfigurationException`.                                                                                                         |

### 4.2 `ReportSyncer.Core.Schema`

_Responsibilities: Runtime inspection, Schema Mapping, and Dependency Resolution._


| **Type**    | **Name**                          | **Responsibility**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| ----------- | --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `interface` | **`ISchemaInspector`**            | Contract to fetch `TableSchema` (columns, types, PKs) from a live DB.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `class`     | **`SqlServerSchemaInspector`**    | Implementation querying `sys.tables`, `sys.columns`, etc.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| `class`     | **`SchemaMapper`**                | Compares source/target schemas. **Crucial:** Implements logic to inject "Context Columns" (e.g., `CustomerId`) if `Source.Type=Application` and `Target.Type=Reporting`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `interface` | **`IDependencyResolver`**         | Given the set of participating `TableTask` definitions and the inspected `TableSchema` / `ForeignKeyRelation` metadata, builds a dependency graph for the job. Produces a safe delete/insert execution order (DAG), detects cycles, and detects **missing upstream tables**. If a selected table has a foreign key to a Target table that is not part of the job's table set, the resolver must signal a fatal schema mismatch (e.g., `SchemaMismatchException`) including which tables are missing and which selected tables depend on them.<br><br>It also must read the `TableTaskConfig.ignoreDependencies` flag and filter the edges in the dependency graph accordingly. |
| `class`     | **`SqlServerDependencyResolver`** | Builds DAG from `sys.foreign_keys` to determine Delete/Insert order. Detects cycles.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |

### 4.3 `ReportSyncer.Core.Security`

_Responsibilities: Guardrails, Permissions, and Safety._

| **Type**    | **Name**                  | **Responsibility**                                                                                                                                |
| ----------- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `interface` | **`IPermissionProfiler`** | Probes DB permissions (CanDelete, CanInsert) using safe transactions (rollback).                                                                  |
| `class`     | **`SafetyValidator`**     | **The Gatekeeper.** Enforces `forbidProdToProd`, `requireDifferentConnections`, and `confirmLargeDeletePct`. Checks `allowAllDelete` requirement. |
| `class`     | **`PermissionsProfile`**  | Model holding boolean flags (`CanDelete`, `CanSetIdentityInsert`) for a table.                                                                    |

### 4.4 `ReportSyncer.Core.Sync`

_Responsibilities: Execution, Data Movement, and Transaction Management._

| **Type**    | **Name**                    | **Responsibility**                                                                                        |
| ----------- | --------------------------- | --------------------------------------------------------------------------------------------------------- |
| `interface` | **`ISyncOrchestrator`**     | **Entry Point.** Coordinates the full job lifecycle (PreFlight -> Plan -> Execute). Handles cancellation. |
| `class`     | **`PreFlightValidator`**    | Runs the validation gauntlet. Throws `SafetyViolationException` or `SchemaMismatchException`.             |
| `interface` | **`ITableRunner`**          | Manages the sync for a single table. Handles `IdentityInsertManager` scope.                               |
| `interface` | **`IDataWriter`**           | Contract for low-level DML (Delete/Insert).                                                               |
| `class`     | **`SqlDataWriter`**         | Generates and executes SQL. Handles Chunked Deletes and Batched Inserts/TVP.                              |
| `class`     | **`IdentityInsertManager`** | Manages `SET IDENTITY_INSERT ON/OFF`. Implements `IDisposable` to guarantee `OFF` is called.              |

### 4.5 `ReportSyncer.Core.Observability`

_Responsibilities: Logging, Progress Tracking, and History._

| **Type**    | **Name**                   | **Responsibility**                                              |
| ----------- | -------------------------- | --------------------------------------------------------------- |
| `class`     | **`ProgressTracker`**      | Calculates throughput (EMA) and ETA. Pure logic (no I/O).       |
| `interface` | **`IJobProgressReporter`** | Abstract sink for progress events (Console or IPC).             |
| `class`     | **`LogHistoryService`**    | Reads/Writes `RunSummary` JSON files for the "History" UI view. |

## 5. Interface Contract (IPC)

Communication between Frontend (Electron) and Backend (WebAPI/Console) uses **JSON-RPC** over Named Pipes or StdIO.

For `ReportSyncer.Wpf`. - **WPF 模式下无需 IPC：** 界面通过依赖注入 (DI) 直接获取 `ISyncOrchestrator` 实例。
- **进度监听：** WPF 层实现 `IJobProgressReporter` 接口。Core 在执行时调用该接口，WPF 实现类负责将数据分发给 UI 线程的 ViewModel。

**Common Envelope:**

```json
{
  "jsonrpc": "2.0",
  "method": "<method_name>",
  "params": { ... },
  "id": 1
}
```

### 5.1 Commands (Frontend -> Backend)

#### `startJob`

Triggers a sync job.

```
{
  "method": "startJob",
  "params": {
    "jobName": "Sync-AGV-Dimensions",
    "dryRun": false,
    "parameters": {
      "CustomerId": "50"
    }
  },
  "id": 101
}
```

#### `cancelJob`

Signals the backend to cancel the running token.

```
{
  "method": "cancelJob",
  "params": {
    "jobId": "run-20231027-xyz"
  },
  "id": 102
}
```

#### `getHistory`

Retrieves past run summaries.

```json
{
  "method": "getHistory",
  "params": {
    "count": 10
  },
  "id": 103
}
```

### 5.2 Notifications (Backend -> Frontend)

#### `jobProgress`

Sent periodically (e.g., every 500ms or batch completion).

```json
{
  "method": "jobProgress",
  "params": {
    "jobId": "run-20231027-xyz",
    "tableName": "dbo.Orders",
    "phase": "Insert",
    "rowsProcessed": 1500,
    "totalRowsPlanned": 5000,
    "percentComplete": 30.0,
    "throughputRowsPerSec": 250,
    "etaSeconds": 14
  }
}
```

#### `jobCompleted`

Sent when execution finishes (Success, Failed, or Cancelled).

```
{
  "method": "jobCompleted",
  "params": {
    "jobId": "run-20231027-xyz",
    "status": "Success",
    "totalRowsDeleted": 0,
    "totalRowsInserted": 5000,
    "durationMs": 21000,
    "errors": []
  }
}
```

## 6. Error Model & Exceptions

The Domain must strictly use these exceptions. Low-level SQL exceptions must be caught and wrapped in `ReportSyncer.Core.Infrastructure` or `Sync`.

| **Exception Type**             | **Trigger Condition**                                                                  | **Recoverable?**                                         |
| ------------------------------ | -------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| **`ConfigurationException`**   | Malformed YAML, missing required fields, invalid parameter types.                      | **No.** User must fix config.                            |
| **`SafetyViolationException`** | Prod-to-Prod attempt, `allowAllDelete=false` violation, Source/Target same connection. | **No.** User must acknowledge/override.                  |
| **`SchemaMismatchException`**  | Missing columns, incompatible types (fail policy), Context Column ambiguity.           | **No.** Schema intervention required.                    |
| **`SyncExecutionException`**   | DB Timeout, Login Failed, Transaction Deadlock, SQL Constraint Violation.              | **Retryable** via `IRetryPolicy` (for transient errors). |
| **`UserCancelledException`**   | Explicit cancellation request via IPC/CLI.                                             | **No.** (Normal flow).                                   |

## 7. Example Configuration (YAML)

This file represents the canonical structure required by `IConfigurationLoader`.

```YAML
version: "1.2"

# ==============================================================================
# ReportSyncer - Complete Configuration Example
# ==============================================================================
# This file demonstrates all configuration features with real-world scenarios.
# Each job illustrates a specific use case with detailed annotations.
#
# KEY CONCEPTS:
# - filter.keyColumn/value: Controls which records to SELECT (source) and DELETE (target)
# - filter.dateColumn/startDate/endDate: Time-based filtering (can combine with keyColumn)
# - keys.businessKey: Defines columns for deduplication/merge logic (not for filtering)
# - columnMapping.mappings: Explicit column mapping with fromSource/const/fromParameter/ignore
# - Context Injection: Automatic when Source=Application & Target=Reporting (or use explicit mapping)
# ==============================================================================

# ==============================================================================
# 1. GLOBAL RUNTIME & SAFETY SETTINGS
# ==============================================================================
run:
  dryRun: false
  defaultBatchSize: 2000
  deleteChunkSize: 5000
  useTvpIfAvailable: true
  etaSmoothing: 0.05

safety:
  # Prevents accidentally syncing Prod to Prod
  forbidProdToProd: true
  # Prevents self-sync (Source == Target)
  requireDifferentConnections: true
  # Requires confirmation if delete affects > 80% of rows
  confirmLargeDeletePct: 0.8

schemaPolicy:
  # Strict mode: fail if columns don't match exactly
  onMismatch: Fail
  requirePrimaryKey: true
  allowExtraTargetColumns: true

# ==============================================================================
# 2. CONNECTIONS
# ==============================================================================
connections:
  # Single-Tenant Application Database (Source of Truth)
  - name: App_Prod_TenantA
    connectionString: "Server=10.0.0.1;Database=AGV_App_TenantA;..."
    environment: Prod
    type: Application

  # Multi-Tenant Reporting Database (Production)
  - name: Report_Prod
    connectionString: "Server=10.0.0.2;Database=AGV_Reporting;..."
    environment: Prod
    type: Reporting

  # Development Reporting Database
  - name: Report_Dev
    connectionString: "Server=localhost;Database=AGV_Reporting_Dev;..."
    environment: Dev
    type: Reporting

# ==============================================================================
# 3. SYNC JOB SCENARIOS
# ==============================================================================
syncJobs:
  # ============================================================================
  # SCENARIO A: Full Table Replacement (Dimension Sync)
  # ============================================================================
  # USE CASE: Syncing reference/dimension tables with no filters
  # WHEN TO USE: 
  #   - Small lookup tables (ProductCategories, Countries, etc.)
  #   - No tenant ID or time-based partitioning
  #   - Full refresh pattern
  # SAFETY: Requires allowAllDelete=true since filter is null
  # ============================================================================
  - name: "Scenario-A-Full-Table-Replacement"
    description: "Full wipe and replace for dimension tables"
    sourceConnection: Report_Prod
    targetConnection: Report_Dev
    parameters: {}

    tables:
      - source: dbo.ProductCategories
        target: dim.ProductCategories
        enabled: true
        preSyncTargetAction: true
        allowAllDelete: true  # REQUIRED for unfiltered deletes
        enableIdentityInsert: true
        filter: null  # No filter = full table sync

        # Optional: Use identity insert to preserve IDs
        columnMapping:
          automapByName: true

  # ============================================================================
  # SCENARIO B: Time-Range + Key Filter (Incremental Sync)
  # ============================================================================
  # USE CASE: Syncing historical data for specific time period and customer
  # WHEN TO USE:
  #   - Incremental/partial data sync
  #   - Multi-tenant data with time-based partitioning
  #   - Need to limit scope by both date and tenant/key
  # FILTER BEHAVIOR:
  #   - SELECT source WHERE (date BETWEEN X AND Y) AND (CustomerId = Z)
  #   - DELETE target WHERE (date BETWEEN X AND Y) AND (CustomerId = Z)
  # ============================================================================
  - name: "Scenario-B-Incremental-Time-And-Key-Filter"
    description: "Sync specific date range for a customer"
    sourceConnection: Report_Prod
    targetConnection: Report_Dev

    parameters:
      StartDate: "2023-10-01"
      EndDate: "2023-10-07"
      CustomerId: "50"

    tables:
      - source: dbo.VehicleLogs
        target: dbo.VehicleLogs
        enabled: true
        preSyncTargetAction: true
        allowAllDelete: false  # Not needed - filter is present

        # Composite filter: date range AND key constraint
        filter:
          dateColumn: VehicleHistoryTime
          startDate: "{StartDate}"
          endDate: "{EndDate}"
          keyColumn: CustomerId
          value: "{CustomerId}"

        # Business key for deduplication
        keys:
          businessKey: [LogId]

        syncOptions:
          batchSize: 5000

  # ============================================================================
  # SCENARIO C: Application → Reporting with Context Injection
  # ============================================================================
  # USE CASE: Moving single-tenant app data to multi-tenant reporting DB
  # WHEN TO USE:
  #   - Source DB is single-tenant (no CustomerId column)
  #   - Target DB is multi-tenant (requires CustomerId column)
  #   - Need to inject tenant context during sync
  # CONTEXT INJECTION OPTIONS:
  #   Option 1: Automatic (SchemaMapper detects App→Reporting)
  #   Option 2: Explicit columnMapping.fromParameter (recommended for clarity)
  # ============================================================================
  - name: "Scenario-C-App-To-Reporting-Context-Injection"
    description: "Inject CustomerId when syncing from single-tenant to multi-tenant"
    sourceConnection: App_Prod_TenantA
    targetConnection: Report_Prod

    parameters:
      CustomerId: 101
      SyncTimestamp: "2025-12-28T10:00:00Z"

    tables:
      - source: dbo.Orders
        target: dbo.Orders
        enabled: true
        preSyncTargetAction: false  # Append-only mode
        enableIdentityInsert: false

        # Explicit column mapping (recommended over automatic injection)
        columnMapping:
          automapByName: true
          mappings:
            # Inject tenant context from parameter
            CustomerId:
              fromParameter: "CustomerId"
            # Add sync metadata
            SyncTimestamp:
              fromParameter: "SyncTimestamp"
            # Fixed values
            SourceSystem:
              const: "AGV_App"
            DataVersion:
              const: "v1"

        # Business key for deduplication (must include CustomerId in target)
        keys:
          businessKey: [OrderId, CustomerId]

  # ============================================================================
  # SCENARIO D: Reporting → Reporting with Key Filter (Pass-Through)
  # ============================================================================
  # USE CASE: Copying filtered data between reporting environments
  # WHEN TO USE:
  #   - Both source and target are multi-tenant
  #   - CustomerId already exists in source
  #   - No context injection needed
  # FILTER BEHAVIOR:
  #   - SELECT source WHERE CustomerId = {param}
  #   - DELETE target WHERE CustomerId = {param}
  # ============================================================================
  - name: "Scenario-D-Reporting-To-Reporting-Filtered"
    description: "Copy specific tenant data from Prod to Dev"
    sourceConnection: Report_Prod
    targetConnection: Report_Dev

    parameters:
      TenantToSync: 101
      StartDate: "2025-01-01"
      EndDate: "2025-12-31"

    tables:
      - source: dbo.Orders
        target: dbo.Orders
        enabled: true
        preSyncTargetAction: true
        allowAllDelete: false

        # Simple key filter (CustomerId already exists in source)
        filter:
          keyColumn: CustomerId
          value: "{TenantToSync}"

        columnMapping:
          automapByName: true
          # No parameter injection needed - CustomerId copies from source

        keys:
          businessKey: [OrderId, CustomerId]

      - source: dbo.OrderLines
        target: dbo.OrderLines
        enabled: true
        preSyncTargetAction: true
        allowAllDelete: false

        # Same key filter for related table
        filter:
          keyColumn: CustomerId
          value: "{TenantToSync}"

        keys:
          businessKey: [OrderLineId, CustomerId]

  # ============================================================================
  # SCENARIO E: Advanced Mapping with Multiple Rules
  # ============================================================================
  # USE CASE: Complex column transformations and explicit mapping
  # WHEN TO USE:
  #   - Need to rename columns
  #   - Add constants or computed values
  #   - Mix of auto-mapping and explicit rules
  #   - Ignore specific columns
  # DEMONSTRATES: All columnMapping rule types
  # ============================================================================
  - name: "Scenario-E-Advanced-Column-Mapping"
    description: "Demonstrates all mapping rule types"
    sourceConnection: Report_Prod
    targetConnection: Report_Dev

    parameters:
      ProcessingDate: "2025-12-28"
      BatchId: "BATCH-001"
      ProcessorName: "ETL-Worker-01"

    tables:
      - source: dbo.RawEvents
        target: dbo.ProcessedEvents
        enabled: true
        preSyncTargetAction: false
        enableIdentityInsert: false

        # Complex filter: date range only
        filter:
          dateColumn: EventTimestamp
          startDate: "{ProcessingDate}"
          endDate: "{ProcessingDate}"

        columnMapping:
          automapByName: true  # Auto-map columns with same names
          mappings:
            # Rename: source column → target column
            EventId:
              fromSource: "RawEventId"
            # Constant value
            EventStatus:
              const: "PROCESSED"
            # From job parameter
            ProcessingBatchId:
              fromParameter: "BatchId"
            ProcessingDate:
              fromParameter: "ProcessingDate"
            ProcessorNode:
              fromParameter: "ProcessorName"
            # Ignore target column (won't be populated)
            InternalMetadata:
              ignore: true
            # Auto-mapped: EventTimestamp, EventType, EventData (by name)

        keys:
          businessKey: [EventId]

        syncOptions:
          batchSize: 10000
          useTvp: true

  # ============================================================================
  # SCENARIO F: Date Range Only (No Key Filter)
  # ============================================================================
  # USE CASE: Time-based incremental sync without tenant filtering
  # WHEN TO USE:
  #   - Single-tenant environment
  #   - Global event logs or audit tables
  #   - Only care about time range
  # ============================================================================
  - name: "Scenario-F-Date-Range-Only"
    description: "Sync specific date range across all tenants"
    sourceConnection: Report_Prod
    targetConnection: Report_Dev

    parameters:
      StartDate: "2025-12-01"
      EndDate: "2025-12-07"

    tables:
      - source: dbo.AuditLog
        target: dbo.AuditLog
        enabled: true
        preSyncTargetAction: true
        allowAllDelete: false

        # Date filter only (no key constraint)
        filter:
          dateColumn: AuditTimestamp
          startDate: "{StartDate}"
          endDate: "{EndDate}"

        columnMapping:
          automapByName: true

        keys:
          businessKey: [AuditLogId]

```