#ReportSyncer

| **Version** | **Description**                                                 |
| ----------- | --------------------------------------------------------------- |
| 2.1         | Added Database Type logic and Context Injection rules (Gary Wu) |
| 2.0         | Product Definition Refinement (Implementation Agnostic)         |

## 1. Executive Summary & Elevator Pitch

**ReportSyncer** is a desktop data synchronization tool designed to move data on-demand between SQL Server databases (specifically for the AGV BI environment). It allows users to refresh dimension tables and historical reporting data from a source environment (e.g., Production) to a target environment (e.g., Development) with strict filtering and safeguards.

Unlike replication services, ReportSyncer is run manually and focuses on safe, append-only data movement. **Source data is never altered**, and target deletions are strictly limited to controlled, pre-configured scenarios.

## 2. System Boundaries & Constraints

The system operates as a local desktop application composed of two distinct logical components: a **User Interface (Frontend)** and an **Execution Engine (Backend)**.

### 2.1 System Architecture

- **Local Service Model:** The Backend runs as a local background service controlled exclusively by the Frontend via Inter-Process Communication (IPC).
    
- **Untrusted Input Principle:** The Backend must treat all inputs from the Frontend (configuration files, run commands, parameters) as **untrusted**. It must independently validate all business rules, safety checks, and schema compatibility before executing any database operations.
    
- **State Management:** The Backend is the source of truth for job execution state. The Frontend is a visualization and command layer.
    

### 2.2 Scope & Limitations

- **In-Scope:**
    
    - On-demand sync jobs triggered by a human user.
        
    - Copying entire tables or filtered slices (date ranges, Customer IDs).
        
    - **Context Injection:** Automatically adding tenant/context identifiers (e.g., `CustomerId`) when moving data from single-tenant Application DBs to multi-tenant Reporting DBs.
        
    - Pre-synchronization cleanup (deleting target data) _only_ when explicitly configured.
        
- **Out-of-Scope (Non-Goals):**
    
    - Real-time CDC (Change Data Capture) or continuous replication.
        
    - Bi-directional sync or complex merge/conflict resolution.
        
    - Source-side deletions or modifications.
        
    - Destructive target operations like `TRUNCATE` (unless simulated via safe deletes).
        
    - Automatic scheduling (cron jobs).
        

## 3. Safety & Business Rules (Guardrails)

Safety is the primary feature of ReportSyncer. The system must enforce the following rules strictly. These rules override any user configuration.

### 3.1 The Prime Directive: Source Immutability

- **Rule:** The system acts as "Read-Only" regarding the Source Database.
    
- **Enforcement:** No `DELETE`, `UPDATE`, `TRUNCATE`, or `DROP` commands shall ever be issued against a connection designated as "Source."
    

### 3.2 Environment Safety

- **Rule: Forbid Prod-to-Prod:** The system must prevent synchronization if both Source and Target connections are identified as "Production" environments.
    
- **Rule: Prevent Self-Sync:** A job cannot run if the Source and Target connection strings resolve to the same database.
    

### 3.3 Target Deletion Guardrails

- **Rule: The `allowAllDelete` Latch:** If a job is configured to delete target data before syncing (`preSyncTargetAction=true`), but **no filter** (e.g., Date Range or CustomerId) is provided, the system must **block execution** unless an explicit `allowAllDelete` flag is set to `true` in the configuration.
    
- **Rule: Large Delete Confirmation:** If the number of rows calculated for deletion exceeds a defined safety threshold (e.g., 80% of the table), the system must pause and require explicit user confirmation.
    

### 3.4 Data Integrity

- **Rule: Identity Preservation:** The system must support `IDENTITY_INSERT` handling. When enabled, it must ensure the setting is turned `ON` before insertion and strictly turned `OFF` immediately after, regardless of job success or failure.
    
- **Rule: Atomic/Safe Batches:** Operations should be batched. Deletions must be "chunked" to prevent transaction log explosion.
    

### 3.5 Database Type Logic & Context Injection

The system distinguishes between **Application** databases (often single-tenant or raw source) and **Reporting** databases (multi-tenant aggregates).

- **Rule: Application $\rightarrow$ Reporting Context Injection:**
    
    - If `Source.Type == Application` and `Target.Type == Reporting`:
        
        - The system must automatically inject a **Context Column** (default: `CustomerId`) into the target insert operation.
            
        - The value for this column is derived from the Job Parameters.
            
    - **Ambiguity Guard:** In this specific scenario, if the Source table _already_ contains a column matching the defined Context Column name, the system must **fail validation and abort**. This prevents ambiguity between an existing source value and the job parameter.
        
- **Rule: Reporting $\rightarrow$ Reporting (Pass-through):**
    
    - If `Source.Type == Reporting`, the system acts as a direct copy. It does not automatically inject the Context Column (unless explicitly configured via standard mapping rules). Existing `CustomerId` columns in the source are preserved.
        
- **Rule: Configurable Context Column:**
    
    - While the default column name is `CustomerId`, the system must allow this to be configurable per job (e.g., `SiteId`, `TenantId`) to support future schema requirements.
        

## 4. Functional Requirements (BDD Scenarios)

These scenarios represent the "Source of Truth" for system behavior.

### Scenario: App DB to Report DB (Context Injection)

- **Given** a Source connection of type "Application" (Single Tenant) and a Target of type "Reporting".
    
- **And** the source table `Orders` has no `CustomerId` column.
    
- **And** the job parameter is set to `CustomerId = 50`.
    
- **When** the user runs the sync job.
    
- **Then** the system reads rows from Source `Orders`.
    
- **And** inserts them into Target `Orders` with an additional column `CustomerId` set to `50`.
    
- **And** the operation succeeds.
    

### Scenario: App DB to Report DB (Ambiguity Failure)

- **Given** a Source connection of type "Application" and Target of type "Reporting".
    
- **And** the source table, for example, `Orders` _already_ contains a column named `CustomerId`.
    
- **When** the user runs the sync job.
    
- **Then** the Pre-flight validation fails with an "Ambiguous Context Column" error.
    
- **And** no data is moved. (Reason: We cannot determine whether to use the source value or the job parameter).
    

### Scenario: Report DB to Report DB (Pass-through)

- **Given** a Source connection of type "Reporting" and Target of type "Reporting" (e.g., Prod to Dev).
    
- **And** the source table, for example,  `Orders` has a `CustomerId` column.
    
- **When** the user runs the sync job filtered by a key column, for example, `CustomerId = 50`. or with additional date column with start and end date. Key column and Date column can be defined in YAML. 
    
- **Then** the system copies the data exactly as is.
    
- **And** validates that the source `CustomerId` matches the filter, but does not inject a new column structure.
    

### Scenario: Successful Dimension Table Sync

- **Given** a source system with up-to-date dimension data and a target table that is outdated.
    
- **When** the user runs a sync job with `preSyncTargetAction=true` and a `CustomerId` parameter set.
    
- **Then** the tool performs a **scoped delete** on the target (removing only rows matching that `CustomerId`).
    
- **And** inserts all rows from the source.
    
- **And** the Source data remains untouched.
    

### Scenario: Historical Data Sync with Date Filter

- **Given** a large historical log table in Production (Source).
    
- **When** the user runs a job filtered to the "Last 7 Days" with `preSyncTargetAction=true`.
    
- **Then** the tool calculates the specific date range.
    
- **And** performs a chunked delete on the Target _only_ for that date range.
    
- **And** inserts source data matching that date range.
    
- **And** enables `IDENTITY_INSERT` temporarily to preserve original primary keys.
    

### Scenario: Safety Check – Unscoped Delete Block

- **Given** a job is configured to wipe the target (`preSyncTargetAction=true`) but has **no filters** defined.
    
- **And** the safety flag `allowAllDelete` is set to `false` (default).
    
- **When** the user attempts to run the job.
    
- **Then** the system **blocks execution** during Pre-flight.
    
- **And** returns a strict validation error: "Full table delete prevented by safety rules."
    

### Scenario: Dry-Run Mode

- **Given** a valid sync configuration.
    
- **When** the user runs the job with **Dry-Run** enabled.
    
- **Then** the system performs all connection checks, schema validations, and row counts.
    
- **And** logs the plan (Estimated Inserts/Deletes).
    
- **But** **NO data modification** (Insert/Delete/Update) occurs on the Target.
    

## 5. User Interface (UX) Expectations

The user interacts with the system via a Desktop UI.

### 5.1 Dashboard

- **Landing View:** Displays a summary of available Sync Jobs and Connection statuses.
    
- **Actions:** Users can select a job to "Run" or "Dry Run."
    
- **Feedback:** Indicators show if connections are online/offline.
    

### 5.2 Configuration Editor

- **Goal:** Manage the YAML-based configuration without editing text files manually.
    
- **Connection Management:** Form-based entry for:
    
    - Connection String.
        
    - Environment Tag (Prod/Dev).
        
    - **Database Type** (Application/Reporting).
        
- **Job Definition:** Visual editor to map Source Table $\rightarrow$ Target Table.
    
    - **Context Column Setting:** Field to define the context column name (default `CustomerId`).
        
- **Safety Toggles:** Checkboxes for high-impact settings (`preSyncTargetAction`, `allowAllDelete`).
    
- **Validation:** The UI must validate unique names and required fields before saving.
    

### 5.3 Run Console (Job Runner)

- **Real-Time Progress:** Overall Job Progress bar, Per-Table status, Throughput metrics (Rows/sec), and ETA.
    
- **Live Logs:** A scrolling terminal-like view showing significant events.
    
- **Controls:** Cancel Button (Immediate), Dry Run Toggle.
    

### 5.4 History View

- **Audit Trail:** A searchable list of past runs (Timestamp, Duration, Rows Transferred, Outcome).
    
- **Error Reporting:** Detailed error messages for failed runs.
    

## 6. Data & Configuration Model

The system behavior is driven by a configuration file (YAML format). The logical entities are:

1. **Run Config:** Global settings (Batch sizes, Dry-run defaults).
    
2. **Schema Policy:** Rules for handling schema drift.
    
3. **Safety Config:** Global thresholds (e.g., `confirmLargeDeletePct`).
    
4. **Connections:** - `Name`
    
    - `ConnectionString`
        
    - `Environment` (Prod, Dev, etc.)
        
    - **`Type`** (Application, Reporting) - _Used to determine context injection logic._
        
5. **Sync Jobs:** Groupings of tasks.
    
    - **Context Configuration:**
        
        - `ContextColumnName` (String, Default: "CustomerId") - _The name of the column to inject/filter by._
            
    - **Table Task:** A single unit of work (Source Table $\rightarrow$ Target Table).
        
        - **Filter:** Optional logic to slice data.
            
        - **Mapping:** Column matching rules.
            
        - **Actions:** `preSyncTargetAction`, `enableIdentityInsert`.
            

## 7. Glossary

- **Application DB:** A source-of-truth database, typically single-tenant or raw application data.
    
- **Reporting DB:** A destination database designed for BI/Reporting, typically multi-tenant (requires `CustomerId` context).
    
- **Context Column:** The column used to segregate data in the Reporting DB (e.g., `CustomerId`, `TenantId`).
    
- **Dimension Table:** Small reference data (Products, Locations). Usually synced via "Delete All + Insert".
    
- **Historical Table:** Large transactional logs. Synced via "Date Slice Delete + Insert".
    
- **Dry Run:** A simulation mode that validates logic and permissions without modifying data.
    
- Pre-Flight: A validation phase that runs before any data modification begins. checks schema, permissions, and safety.
    



