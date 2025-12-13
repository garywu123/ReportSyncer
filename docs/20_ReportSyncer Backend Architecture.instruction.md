
## 1. Technology Stack & Constraints

All backend development must adhere to the following technology choices. No unauthorized libraries are permitted.

| **Category**             | **Choice**                                 | **Notes**                                                                                                               |
| ------------------------ | ------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------- |
| **Framework**            | **.NET 9**                                 | Target `net9.0` for all projects.                                                                                       |
| **Language**             | C# 12/13                                   | Use modern features (records, pattern matching) where appropriate.                                                      |
| **Dependency Injection** | `Microsoft.Extensions.DependencyInjection` | Abstractions only in Core. `DryIoc` allowed in Host composition roots only.                                             |
| **Logging**              | `Serilog`                                  | `ReportSyncer.Core` depends on `DotNetToolkit.Logging.ILogService`. Hosts configure Serilog sinks (Console, File, IPC). |
| **Database Access**      | `DotNetToolkit.Database`                   | **NO EF Core.** Use provided toolkit abstractions (`IDbContext`, `IDbCommandWrapper`) + raw SQL for bulk ops.           |
| **Configuration**        | `YamlDotNet`                               | Used in `ReportSyncer.Core.Configuration` to parse YAML.                                                                |
| **Serialization**        | `Newtonsoft.Json`                          | Standard for IPC messages and history files.                                                                            |
| **Testing**              | `xUnit`, `FluentAssertions`, `Moq`         | Unit tests for logic; Integration tests (LocalDB) for data access.                                                      |

## 2. Project Layout & Responsibilities

The solution uses a **layered, domain-centric** architecture.

### 2.1 Project List

|   |   |   |   |
|---|---|---|---|
|**Project**|**Type**|**Category**|**Responsibility**|
|**`ReportSyncer.Core`**|Class Lib|**Domain**|**The Brain.** Contains all business logic: config parsing, schema inspection, safety validation, sync orchestration, and SQL generation logic.|
|**`ReportSyncer.Console`**|EXE|**Host**|**The Runner.** CLI host for batch/ops. Bootstraps DI, loads config, wires logging, and calls `ISyncOrchestrator`.|
|**`ReportSyncer.WebApi`**|EXE|**Host**|**The API.** (Future) HTTP host for Electron/React. Exposes endpoints for UI to control the Core. Handles Auth/DTO mapping only.|
|**`ReportSyncer.Tests`**|Test Proj|**Test**|Unit and Integration tests. Contains fixtures for LocalDB and seeded data.|
|**`DotNetToolkit.General`**|Class Lib|**Shared**|Domain-agnostic utilities (Guard clauses, functional extensions).|
|**`DotNetToolkit.Logging`**|Class Lib|**Shared**|Logging abstractions (`ILogService`) and generic adapters.|
|**`DotNetToolkit.Database`**|Class Lib|**Shared**|Low-level DB helpers (`IDbContext`, connection factories, retry policies).|

### 2.2 Dependency Rules

**Strict Layering Enforcement:**

```
ALLOWED References:
  ReportSyncer.Core     -> DotNetToolkit.*
  ReportSyncer.Console  -> ReportSyncer.Core, DotNetToolkit.*
  ReportSyncer.WebApi   -> ReportSyncer.Core, DotNetToolkit.*
  ReportSyncer.Tests    -> ReportSyncer.Core, ReportSyncer.Console, DotNetToolkit.*

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
| `class`     | **`TableTaskConfig`**         | Config for a single table sync. Includes `source`, `target`, `preSyncTargetAction`, `allowAllDelete`, `filter`.                                                                                                           |
| `class`     | **`FilterConfig`**            | Represents optional table filter. Can define a key-based scope (`keyColumn`/`value`), a date range (`dateColumn`/`startDate`/`endDate`), or **both** (combined with `AND` logic). Used by SQL builder and safety logic.\| |
| `interface` | **`IConfigurationLoader`**    | Contract for loading config from disk/stream.                                                                                                                                                                             |
| `class`     | **`YamlConfigurationLoader`** | Implementation using `YamlDotNet`. Handles file I/O and deserialization errors.                                                                                                                                           |
| `class`     | **`ConfigurationValidator`**  | Enforces static rules (e.g., unique names, required fields, basic safety flags). Throws `ConfigurationException`.                                                                                                         |

### 4.2 `ReportSyncer.Core.Schema`

_Responsibilities: Runtime inspection, Schema Mapping, and Dependency Resolution._


| **Type**    | **Name**                          | **Responsibility**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ----------- | --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `interface` | **`ISchemaInspector`**            | Contract to fetch `TableSchema` (columns, types, PKs) from a live DB.                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `class`     | **`SqlServerSchemaInspector`**    | Implementation querying `sys.tables`, `sys.columns`, etc.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `class`     | **`SchemaMapper`**                | Compares source/target schemas. **Crucial:** Implements logic to inject "Context Columns" (e.g., `CustomerId`) if `Source.Type=Application` and `Target.Type=Reporting`.                                                                                                                                                                                                                                                                                                                                                                      |
| `interface` | **`IDependencyResolver`**         | Given the set of participating `TableTask` definitions and the inspected `TableSchema` / `ForeignKeyRelation` metadata, builds a dependency graph for the job. Produces a safe delete/insert execution order (DAG), detects cycles, and detects **missing upstream tables**. If a selected table has a foreign key to a Target table that is not part of the job's table set, the resolver must signal a fatal schema mismatch (e.g., `SchemaMismatchException`) including which tables are missing and which selected tables depend on them. |
| `class`     | **`SqlServerDependencyResolver`** | Builds DAG from `sys.foreign_keys` to determine Delete/Insert order. Detects cycles.                                                                                                                                                                                                                                                                                                                                                                                                                                                          |

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
version: 1.2

# ==============================================================================
# 1. GLOBAL RUNTIME & SAFETY SETTINGS
# ==============================================================================
run:
  dryRun: false
  defaultBatchSize: 2000
  deleteChunkSize: 5000
  useTvpIfAvailable: true  # Optimization enabled

safety:
  # Prevents accidentally syncing Prod to Prod (e.g., Report_Prod -> App_Prod)
  forbidProdToProd: true
  
  # Prevents self-sync (Source == Target)
  requireDifferentConnections: true
  
  # If a delete operation affects > 80% of rows, the UI/Backend demands manual confirmation
  confirmLargeDeletePct: 0.8

schemaPolicy:
  # strict mode: fail if columns don't match exactly
  onMismatch: fail 
  requirePrimaryKey: true
  allowExtraTargetColumns: true

# ==============================================================================
# 2. CONNECTIONS
# ==============================================================================
connections:
  # SCENARIO: Single Tenant Application Database (Source of Truth)
  - name: App_Prod_TenantA
    connectionString: "Server=10.0.0.1;Database=AGV_App_TenantA;..."
    environment: Prod
    type: Application # <--- Triggers Context Injection logic in SchemaMapper

  # SCENARIO: Multi-Tenant Reporting Database (Production)
  - name: Report_Prod
    connectionString: "Server=10.0.0.2;Database=AGV_Reporting;..."
    environment: Prod
    type: Reporting   # <--- Destination for aggregated data

  # SCENARIO: Development Reporting Database (Target for testing)
  - name: Report_Dev
    connectionString: "Server=localhost;Database=AGV_Reporting_Dev;..."
    environment: Dev
    type: Reporting

# ==============================================================================
# 3. JOB SCENARIOS
# ==============================================================================
syncJobs:

  # ----------------------------------------------------------------------------
  # SCENARIO A: Dimension Table Sync (Full Wipe & Replace)
  # Use Case: Syncing "Product Categories" from Prod to Dev.
  # Challenge: The table has no TenantID/Date, so we must allow a full table wipe.
  # ----------------------------------------------------------------------------
  - name: "Sync-Dimensions-ProdToDev"
    description: "Refreshes reference data. DESTRUCTIVE: Wipes target table."
    sourceConnection: Report_Prod
    targetConnection: Report_Dev
    
    parameters: {} # No parameters needed for global dimensions

    tables:
      - source: dbo.ProductCategories
        target: dim.ProductCategories
        enabled: true
        
        # 1. Delete everything in target before inserting
        preSyncTargetAction: true 
        
        # 2. REQUIRED SAFETY FLAG: 
        # Because 'filter' is null, the backend will BLOCK this job unless
        # allowAllDelete is explicitly set to true.
        allowAllDelete: true
        
        enableIdentityInsert: true
        filter: null

  # ----------------------------------------------------------------------------
  # SCENARIO B: Historical Data Sync (Time-Sliced AND Key-Filtered)
  # Use Case: Copying last week's logs for a specific Customer to Dev.
  # Challenge: We need to filter by BOTH date range AND CustomerId.
  # ----------------------------------------------------------------------------
  - name: "Sync-Logs-LastWeek-Customer50"
    description: "Copies vehicle logs for a specific date range and customer."
    sourceConnection: Report_Prod
    targetConnection: Report_Dev

    # Parameters passed from UI or CLI
    parameters:
      StartDate: "2023-10-01"
      EndDate: "2023-10-07"
      CustomerId: "50"

    tables:
      - source: dbo.VehicleLogs
        target: dbo.VehicleLogs
        preSyncTargetAction: true
        allowAllDelete: false 
        
        # PROPOSED DESIGN: Composite Filter
        # Supports simultaneous Date Range AND Key Column filtering.
        # Logic: WHERE (VehicleHistoryTime BETWEEN X AND Y) AND (CustomerId = Z)
        filter:
          # Part 1: Date Range
          dateColumn: VehicleHistoryTime
          startDate: "{StartDate}"
          endDate: "{EndDate}"
          
          # Part 2: Key Constraint
          keyColumn: CustomerId
          value: "{CustomerId}"

        syncOptions:
          batchSize: 10000 

  # ----------------------------------------------------------------------------
  # SCENARIO C: Application -> Reporting (Context Injection)
  # Use Case: Moving data from a Single-Tenant App DB to a Multi-Tenant Report DB.
  # Challenge: Source table has no 'CustomerId', but Target requires it.
  # Logic: Because Source.Type=Application and Target.Type=Reporting,
  #        SchemaMapper AUTOMATICALLY injects the parameter into the insert.
  # ----------------------------------------------------------------------------
  - name: "Ingest-TenantA-Orders"
    sourceConnection: App_Prod_TenantA
    targetConnection: Report_Prod
    
    parameters:
      # This value is injected into the 'CustomerId' column on Target
      CustomerId: 101 

    tables:
      - source: dbo.Orders
        target: dbo.Orders
        preSyncTargetAction: false # Append only (Continuous ingestion scenario)
        
        # We assume dbo.Orders in App DB does NOT have CustomerId.
        # We assume dbo.Orders in Report DB DOES have CustomerId.
        
        # Explicit mapping ensures we map columns by name, 
        # plus the auto-injection handled by the backend logic.
        columnMapping:
          automapByName: true
          
        # Deduping logic: Don't insert if OrderId + CustomerId already exists
        keys:
          businessKey: [OrderId]

  # ----------------------------------------------------------------------------
  # SCENARIO D: Reporting -> Reporting (Pass-Through)
  # Use Case: Copying data from Prod Reporting to Dev Reporting.
  # Logic: Both connections are 'Reporting'. Context Injection is DISABLED.
  #        The 'CustomerId' column in source is copied as-is to target.
  # ----------------------------------------------------------------------------
  - name: "Sync-Reporting-ProdToDev-Filtered"
    sourceConnection: Report_Prod
    targetConnection: Report_Dev
    
    parameters:
      TenantToSync: 101

    tables:
      - source: dbo.Orders
        target: dbo.Orders
        preSyncTargetAction: true
        
        # We strictly filter by the existing CustomerId column
        filter:
          keyColumn: CustomerId
          value: "{TenantToSync}"
          
        columnMapping:
          automapByName: true
          # No injection happens here because Source is Type=Reporting
		
		source: dbo.HistoricalVehicles
	    target: dbo.HistoricalVehicles
        preSyncTargetAction: true
        allowAllDelete: false # Safety: We do NOT want to wipe the whole log table
        
        # Filter applies to BOTH the Source Select and the Target Pre-Sync Delete
        filter:
          dateColumn: VehicleHistoryTime
          startDate: "{StartDate}"
          endDate: "{EndDate}"
        
        syncOptions:
          batchSize: 10000 # Larger batch for log data

  # ----------------------------------------------------------------------------
  # SCENARIO C: Application -> Reporting (Context Injection)
  # Use Case: Moving data from a Single-Tenant App DB to a Multi-Tenant Report DB.
  # Challenge: Source table has no 'CustomerId', but Target requires it.
  # Logic: Because Source.Type=Application and Target.Type=Reporting,
  #        SchemaMapper AUTOMATICALLY injects the parameter into the insert.
  # ----------------------------------------------------------------------------
  - name: "Ingest-TenantA-Orders"
    sourceConnection: App_Prod_TenantA
    targetConnection: Report_Prod
    
    parameters:
      # This value is injected into the 'CustomerId' column on Target
      CustomerId: 101 

    tables:
      - source: dbo.Orders
        target: dbo.Orders
        preSyncTargetAction: false # Append only (Continuous ingestion scenario)
        
        # We assume dbo.Orders in App DB does NOT have CustomerId.
        # We assume dbo.Orders in Report DB DOES have CustomerId.
        
        # Explicit mapping ensures we map columns by name, 
        # plus the auto-injection handled by the backend logic.
        columnMapping:
          automapByName: true
          
        # Deduping logic: Don't insert if OrderId + CustomerId already exists
        keys:
          businessKey: [OrderId]

  # ----------------------------------------------------------------------------
  # SCENARIO D: Reporting -> Reporting (Pass-Through)
  # Use Case: Copying data from Prod Reporting to Dev Reporting.
  # Logic: Both connections are 'Reporting'. Context Injection is DISABLED.
  #        The 'CustomerId' column in source is copied as-is to target.
  # ----------------------------------------------------------------------------
  - name: "Sync-Reporting-ProdToDev-Filtered"
    sourceConnection: Report_Prod
    targetConnection: Report_Dev
    
    parameters:
      TenantToSync: 101

    tables:
      - source: dbo.Orders
        target: dbo.Orders
        preSyncTargetAction: true
        
        # We strictly filter by the existing CustomerId column
        filter:
          keyColumn: CustomerId
          value: "{TenantToSync}"
          
        columnMapping:
          automapByName: true
          # No injection happens here because Source is Type=Reporting
```