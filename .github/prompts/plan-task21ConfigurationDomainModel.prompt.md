# Task 2.1 Implementation Plan – Configuration Domain Model

## Overview

Create strongly-typed, immutable domain models representing the entire YAML configuration structure with comprehensive safety and context-injection support. These models will serve as the canonical representation consumed by all downstream modules (schema, safety, sync), eliminating any need for raw YAML access.

## Technical Constraints

- **Target Framework**: .NET 8.0 (current)
- **Language**: C# 8.0 features only
- **Namespace**: `ReportSyncer.Core.Configuration`
- **Immutability**: Constructor-only initialization with readonly fields (no `init` accessors - C# 9+ feature)
- **Dependencies**: 
  - `DotNetToolkit.General` (Guard, Result<T>)
  - `DotNetToolkit.Logging` (ILogService)
  - No YamlDotNet dependency yet (deferred to Task 2.2)
  - No `DotNetToolkit.Database` usage in configuration namespace

## Implementation Steps

### Step 1: Define Enumerations and Value Types

Create type-safe enumerations for fixed vocabularies:

**Files to create:**
- `ReportSyncer.Core/Configuration/EnvironmentType.cs`
- `ReportSyncer.Core/Configuration/ConnectionType.cs`
- `ReportSyncer.Core/Configuration/SchemaMismatchBehavior.cs`

**Requirements:**
- `EnvironmentType`: Prod, Dev, Test, Staging, etc.
- `ConnectionType`: Application, Reporting
- `SchemaMismatchBehavior`: Fail, Warn, Ignore (or similar)
- XML documentation explaining each enum value
- File headers: Author (Gary Wu), Project (ReportSyncer), Date (2025-12-01)

### Step 2: Implement Leaf Configuration Models

Create the foundational configuration models that have no dependencies on other config types:

**Files to create:**
- `ReportSyncer.Core/Configuration/KeyConfig.cs`
- `ReportSyncer.Core/Configuration/SyncOptionsConfig.cs`
- `ReportSyncer.Core/Configuration/FilterConfig.cs`
- `ReportSyncer.Core/Configuration/AddedColumnMappingConfig.cs`
- `ReportSyncer.Core/Configuration/ColumnMappingConfig.cs`

**KeyConfig requirements:**
- `BusinessKey` property: `IReadOnlyList<string>` (column names for composite key)
- Constructor validation: non-null, non-empty list
- XML docs: explain deduplication usage

**SyncOptionsConfig requirements:**
- `BatchSize` property: `int?` (nullable for optional override)
- `UseTvp` property: `bool?` (nullable for optional override)
- XML docs: explain table-level override behavior

**FilterConfig requirements:**
- Date range filter properties: `DateColumn`, `StartDate`, `EndDate` (all nullable strings)
- Key filter properties: `KeyColumn`, `Value` (nullable strings)
- Support parameter placeholders: "{StartDate}", "{EndDate}", "{CustomerId}"
- Constructor: allow null for all properties (filter is optional)
- XML docs with CDATA examples showing parameter placeholder patterns

**AddedColumnMappingConfig requirements:**
- `ColumnName` property: `string` (required)
- `Value` property: `string` (constant or parameter reference like "{CustomerId}")
- Constructor validation: non-null, non-empty columnName
- XML docs: explain context injection usage

**ColumnMappingConfig requirements:**
- `AutomapByName` property: `bool`
- `ExplicitMappings` property: `IReadOnlyDictionary<string, string>` (source→target, nullable)
- `AddedColumns` property: `IReadOnlyList<AddedColumnMappingConfig>` (nullable)
- XML docs: explain automap vs explicit mapping, context column injection

### Step 3: Implement Configuration Models with References

Create models that reference other configuration types:

**Files to create:**
- `ReportSyncer.Core/Configuration/TableTaskConfig.cs`
- `ReportSyncer.Core/Configuration/ConnectionConfig.cs`
- `ReportSyncer.Core/Configuration/SyncJobConfig.cs`

**ConnectionConfig requirements:**
- `Name` property: `string` (unique identifier, required)
- `ConnectionString` property: `string` (required)
- `Environment` property: `EnvironmentType` (required)
- `Type` property: `ConnectionType` (required)
- Constructor validation: non-null, non-empty name and connectionString
- XML docs: explain environment and type usage in safety rules and context injection

**TableTaskConfig requirements:**
- `Source` property: `string` (source table name, required)
- `Target` property: `string` (target table name, required)
- `Enabled` property: `bool` (default true)
- `PreSyncTargetAction` property: `bool` (whether to delete before insert)
- `AllowAllDelete` property: `bool` (safety latch, **defaults to false**)
- `EnableIdentityInsert` property: `bool`
- `Filter` property: `FilterConfig` (nullable)
- `ColumnMapping` property: `ColumnMappingConfig` (nullable)
- `Keys` property: `KeyConfig` (nullable)
- `SyncOptions` property: `SyncOptionsConfig` (nullable)
- Constructor validation: non-null, non-empty source and target
- XML docs with **warning tag** for `AllowAllDelete` default, examples for each property

**SyncJobConfig requirements:**
- `Name` property: `string` (unique, required)
- `Description` property: `string` (nullable)
- `SourceConnection` property: `string` (connection name reference, required)
- `TargetConnection` property: `string` (connection name reference, required)
- `Parameters` property: `IReadOnlyDictionary<string, string>` (e.g., CustomerId, StartDate, EndDate)
- `Tables` property: `IReadOnlyList<TableTaskConfig>` (at least one required)
- Constructor validation: non-null names, non-empty tables list
- XML docs: explain parameter resolution in filters and added columns

### Step 4: Implement Global Settings and Root Configuration

Create top-level configuration models:

**Files to create:**
- `ReportSyncer.Core/Configuration/RunConfig.cs`
- `ReportSyncer.Core/Configuration/SafetyConfig.cs`
- `ReportSyncer.Core/Configuration/SchemaPolicyConfig.cs`
- `ReportSyncer.Core/Configuration/SyncConfiguration.cs`

**RunConfig requirements:**
- `DryRun` property: `bool`
- `DefaultBatchSize` property: `int` (e.g., 2000)
- `DeleteChunkSize` property: `int` (e.g., 5000)
- `UseTvpIfAvailable` property: `bool`
- `EtaSmoothing` property: `double` (optional, nullable)
- Constructor validation: positive batch sizes
- XML docs: explain dry-run semantics, batch vs chunk size

**SafetyConfig requirements:**
- `ForbidProdToProd` property: `bool` (prevents Prod→Prod syncs)
- `RequireDifferentConnections` property: `bool` (prevents self-sync)
- `ConfirmLargeDeletePct` property: `double` (0-1 range, e.g., 0.8 = 80%)
- Constructor validation: confirmLargeDeletePct between 0 and 1
- XML docs with **warning tags** for safety implications

**SchemaPolicyConfig requirements:**
- `OnMismatch` property: `SchemaMismatchBehavior` (fail, warn, etc.)
- `RequirePrimaryKey` property: `bool`
- `AllowExtraTargetColumns` property: `bool`
- XML docs: explain each policy and when it applies

**SyncConfiguration requirements (root model):**
- `Version` property: `string` (e.g., "1.2")
- `Run` property: `RunConfig` (required)
- `Safety` property: `SafetyConfig` (required)
- `SchemaPolicy` property: `SchemaPolicyConfig` (required)
- `Connections` property: `IReadOnlyList<ConnectionConfig>` (at least one required)
- `SyncJobs` property: `IReadOnlyList<SyncJobConfig>` (at least one required)
- Constructor validation: non-null sections, non-empty connections and jobs
- XML docs: comprehensive root-level documentation with CDATA usage example

### Step 5: Add Comprehensive XML Documentation

Apply consistent documentation to all types:

**Documentation standards:**
- File header comment: Author (Gary Wu), Project (ReportSyncer), Date (2025-12-01), purpose
- Class `<summary>`: what it represents, when it's used
- Class `<example>`: usage example in CDATA section
- Property `<summary>`: what it stores, valid values/format
- Property `<remarks>`: special behavior, defaults, safety implications
- Property `<warning>`: safety-critical defaults (e.g., `AllowAllDelete` defaults to false)
- Constructor `<summary>`: what it initializes
- Constructor `<param>`: each parameter's purpose and constraints
- Constructor `<exception>`: what validation failures throw (ArgumentException, ArgumentNullException)
- Consistent terminology aligned with PRD (e.g., "pre-sync delete", "context injection", "parameter placeholder")

## Key Design Decisions

### Immutability Pattern (C# 8.0)
- Use readonly fields with constructor-only initialization
- No `init` accessors (requires C# 9+)
- Collections exposed as `IReadOnlyList<T>` or `IReadOnlyDictionary<K,V>`
- Internal storage can use arrays or List<T>, exposed via readonly interfaces

### Parameter Placeholder Support
- Strings like `"{CustomerId}"`, `"{StartDate}"` stored as-is in domain models
- No resolution/substitution in Task 2.1 (parsing concern for Task 2.2, resolution for runtime)
- Document placeholder pattern in XML docs

### Default Values
- `AllowAllDelete`: **false** (critical safety default)
- `Enabled` (table): true
- `AutomapByName`: true (common case)
- Other bools: explicit required in constructor where safety-critical

### Validation Strategy
- **In Task 2.1**: Only type-level constraints (non-null, non-empty, positive numbers, range checks)
- **NOT in Task 2.1**: Reference integrity, semantic rules, cross-field validation (deferred to Task 2.3)
- Throw `ArgumentException` or `ArgumentNullException` for constructor validation failures

## Success Criteria

✅ All YAML entities have corresponding strongly-typed models  
✅ Models support all scenarios from example YAML (dimension sync, historical sync, context injection, reporting→reporting)  
✅ Models are immutable (readonly fields, read-only collection interfaces)  
✅ Enums used for fixed vocabularies (environment, connection type, schema behavior)  
✅ No YAML parsing logic (pure domain models)  
✅ No validation logic beyond constructor parameter checks (semantic validation is Task 2.3)  
✅ Comprehensive XML documentation with file headers, summaries, examples, warnings  
✅ Models live in `ReportSyncer.Core.Configuration` namespace  
✅ No dependencies on `DotNetToolkit.Database`  
✅ Compatible with .NET 8.0 / C# 8.0  
✅ No `init` accessors or other C# 9+ features  

## Testing Approach (Deferred to Task 2.3)

While full validation testing is Task 2.3, basic constructor/immutability tests can be written:
- Construct valid objects with all required properties
- Verify immutability (collections cannot be modified externally)
- Verify constructor validation throws on invalid inputs (null, empty, negative, out-of-range)
- Defer semantic validation (reference integrity, safety rules) to Task 2.3

## Out of Scope for Task 2.1

❌ YAML loading/parsing (Task 2.2)  
❌ YamlDotNet integration (Task 2.2)  
❌ Configuration validation logic (Task 2.3)  
❌ Reference integrity checks (Task 2.3)  
❌ Safety rule enforcement (Task 2.3)  
❌ Effective configuration & overrides (Task 2.4)  
❌ Enhanced error model with error codes (Task 2.5)  
❌ Parameter placeholder resolution (runtime concern)  

## Implementation Order

1. Enumerations (no dependencies)
2. Leaf models (no config type dependencies)
3. Connection model (uses enums only)
4. Table model (uses leaf models)
5. Job model (uses table model)
6. Global settings (independent)
7. Root configuration (aggregates everything)
8. XML documentation pass (all files)
9. Basic constructor tests (optional for Task 2.1)

## Notes for AI Coder

- Focus on **domain clarity** over premature optimization
- **No smart validation** in constructors beyond obvious type constraints (semantic validation is Task 2.3's job)
- Use **readonly backing fields** and expose via properties (C# 8.0 pattern)
- **IReadOnlyList<T>** / **IReadOnlyDictionary<K,V>** for all collections to enforce immutability
- Follow project coding style exactly (file headers, XML doc tags, CDATA for examples)
- When in doubt about a design choice, prefer **simplicity** and **alignment with YAML structure**
