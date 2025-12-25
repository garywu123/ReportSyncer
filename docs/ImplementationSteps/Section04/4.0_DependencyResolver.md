# Task 4.0 Implementation Playbook: Dependency Planning (IDependencyResolver + ExecutionPlan)

**Document Version:** 1.0  
**Created:** 2025-12-25  
**Author:** Gary Wu  
**Project:** ReportSyncer

---

## 📋 Validation Summary

### ✅ Task 4.0 Is VALID and READY for Implementation

**Verification Results:**

1. **Prerequisites Met:**
   - ✅ `ForeignKeySchema` exists (`ReportSyncer.Core/Schema/ForeignKeySchema.cs`)
   - ✅ `TableSchema` with FK relationships exists
   - ✅ `TableIdentifier` exists and supports equality comparison
   - ✅ `SchemaSnapshot` exists with table collection
   - ✅ `Result<T>` exists in `DotNetToolkit.General`
   - ✅ `SchemaMismatchException` exists for error scenarios

2. **Architectural Alignment:**
   - ✅ Matches PRD Section 3 (Safety & Business Rules - Data Integrity)
   - ✅ Follows backend architecture (Section 3.1 - ReportSyncer.Core responsibilities)
   - ✅ Respects dependency rules (no toolkit dependencies from Core)
   - ✅ Consistent with Section 3's schema subsystem design

3. **Gap Analysis:**
   - ✅ No conflicting implementation exists
   - ✅ Proper namespace location identified (`ReportSyncer.Core.Schema.Dependency`)
   - ✅ Clear interface boundaries defined
   - ✅ Test structure already established (`Tests/ReportSyncer.Core.Tests`)

---

## 🎯 Implementation Goals

### Primary Objectives
1. Build FK dependency graph from Target schema
2. Detect missing parent tables (dependency closure validation)
3. Perform topological sort with cycle detection
4. Generate safe execution order (insert = parents→children, delete = reverse)

### Non-Goals (Defer to Later Tasks)
- ❌ Runtime execution of delete/insert operations
- ❌ Safety/permission checks
- ❌ Progress tracking
- ❌ History recording

---

## 📂 File Structure to Create

```
ReportSyncer.Core/
  Schema/
    Dependency/
      ├── IDependencyResolver.cs          (NEW)
      ├── DependencyResolver.cs           (NEW)
      ├── ExecutionPlan.cs                (NEW)
      ├── DependencyValidationError.cs    (NEW)
      └── DependencyGraphNode.cs          (NEW - optional helper)

Tests/ReportSyncer.Core.Tests/
  UnitTests/
    Schema/
      Dependency/
        └── DependencyResolverUnitTests.cs    (NEW)
```

---

## 📝 Step-by-Step Implementation Plan

### **Step 1: Create Domain Models**

#### 1.1 Create `ExecutionPlan.cs`

**File:** `ReportSyncer.Core/Schema/Dependency/ExecutionPlan.cs`

**Requirements:**
- Immutable record type
- Contains insert order (parents before children)
- Contains delete order (children before parents)
- Clear XML documentation

**Code Template:**
```csharp
// File: ReportSyncer.Core/Schema/Dependency/ExecutionPlan.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

namespace ReportSyncer.Core.Schema.Dependency;

/// <summary>
/// Represents a validated execution plan for table synchronization operations.
/// The plan defines the order in which tables must be processed to respect
/// foreign key relationships.
/// </summary>
/// <param name="InsertOrder">
/// Tables ordered for safe insertion: parent tables appear before child tables
/// that reference them.
/// </param>
/// <param name="DeleteOrder">
/// Tables ordered for safe deletion: child tables appear before parent tables
/// they reference. This is the reverse of <see cref="InsertOrder"/>.
/// </param>
/// <remarks>
/// <para>
/// An empty list in either order indicates no tables require ordering
/// (e.g., no tables selected or no FK relationships among selected tables).
/// </para>
/// <example>
/// Given FK: Orders → Customers
/// <code>
/// InsertOrder = [Customers, Orders]  // Insert Customers first
/// DeleteOrder = [Orders, Customers]  // Delete Orders first
/// </code>
/// </example>
/// </remarks>
public sealed record ExecutionPlan(
    IReadOnlyList<TableIdentifier> InsertOrder,
    IReadOnlyList<TableIdentifier> DeleteOrder)
{
    /// <summary>
    /// Creates an empty execution plan with no tables.
    /// </summary>
    public static ExecutionPlan Empty => new(
        Array.Empty<TableIdentifier>(),
        Array.Empty<TableIdentifier>()
    );
}
```

**Validation Checklist:**
- [ ] File header matches project standards
- [ ] Record is `sealed`
- [ ] Both parameters are `IReadOnlyList<TableIdentifier>`
- [ ] XML documentation explains insert vs delete order
- [ ] Includes usage example in `<example>` tag
- [ ] Empty static factory method provided

---

#### 1.2 Create `DependencyValidationError.cs`

**File:** `ReportSyncer.Core/Schema/Dependency/DependencyValidationError.cs`

**Requirements:**
- Represent specific dependency errors (missing parent, cycle detected)
- Include enough detail for clear error messages
- Support structured error reporting

**Code Template:**
```csharp
// File: ReportSyncer.Core/Schema/Dependency/DependencyValidationError.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

namespace ReportSyncer.Core.Schema.Dependency;

/// <summary>
/// Represents a validation error encountered during dependency analysis.
/// </summary>
public sealed record DependencyValidationError
{
    /// <summary>
    /// Type of dependency error encountered.
    /// </summary>
    public DependencyErrorKind Kind { get; init; }

    /// <summary>
    /// Primary table involved in the error (e.g., the dependent table).
    /// </summary>
    public TableIdentifier? Table { get; init; }

    /// <summary>
    /// Missing or problematic referenced table (for <see cref="DependencyErrorKind.MissingParent"/>).
    /// </summary>
    public TableIdentifier? ReferencedTable { get; init; }

    /// <summary>
    /// Tables involved in a cycle (for <see cref="DependencyErrorKind.CycleDetected"/>).
    /// </summary>
    public IReadOnlyList<TableIdentifier>? CyclePath { get; init; }

    /// <summary>
    /// Human-readable description of the error.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Creates an error indicating a selected table depends on an unselected parent table.
    /// </summary>
    public static DependencyValidationError MissingParent(
        TableIdentifier dependentTable,
        TableIdentifier missingParent)
    {
        return new DependencyValidationError
        {
            Kind = DependencyErrorKind.MissingParent,
            Table = dependentTable,
            ReferencedTable = missingParent,
            Message = $"Table '{dependentTable}' has a foreign key to '{missingParent}', " +
                      $"but '{missingParent}' is not included in the selected tables for this job. " +
                      $"Either add '{missingParent}' to the job or remove '{dependentTable}'."
        };
    }

    /// <summary>
    /// Creates an error indicating a circular dependency was detected.
    /// </summary>
    public static DependencyValidationError Cycle(IReadOnlyList<TableIdentifier> cyclePath)
    {
        var pathStr = string.Join(" → ", cyclePath.Select(t => t.ToString()));
        return new DependencyValidationError
        {
            Kind = DependencyErrorKind.CycleDetected,
            CyclePath = cyclePath,
            Message = $"Circular dependency detected: {pathStr}. " +
                      $"Cannot determine a safe execution order."
        };
    }
}

/// <summary>
/// Categories of dependency validation errors.
/// </summary>
public enum DependencyErrorKind
{
    /// <summary>
    /// A selected table has a FK to a non-selected table.
    /// </summary>
    MissingParent,

    /// <summary>
    /// A circular FK relationship exists among selected tables.
    /// </summary>
    CycleDetected
}
```

**Validation Checklist:**
- [ ] Record structure supports both error types
- [ ] Factory methods create clear error messages
- [ ] Messages reference both dependent and missing/cycle tables
- [ ] Enum clearly defines error categories

---

### **Step 2: Define Interface Contract**

#### 2.1 Create `IDependencyResolver.cs`

**File:** `ReportSyncer.Core/Schema/Dependency/IDependencyResolver.cs`

**Requirements:**
- Single method contract
- Takes target snapshot + selected tables
- Returns `Result<ExecutionPlan>`
- Failure case includes structured errors

**Code Template:**
```csharp
// File: ReportSyncer.Core/Schema/Dependency/IDependencyResolver.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using DotNetToolkit.General;

namespace ReportSyncer.Core.Schema.Dependency;

/// <summary>
/// Resolves table dependencies and generates a safe execution order
/// for synchronization operations based on foreign key relationships.
/// </summary>
/// <remarks>
/// <para>
/// This resolver enforces two critical rules:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Dependency Closure:</b> If a selected target table has a FK to another
/// target table, that referenced table must also be selected (or the operation fails).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Acyclic Graph:</b> Circular FK relationships are not supported and cause failure.
/// </description>
/// </item>
/// </list>
/// <para>
/// The resolver only examines FK relationships in the <paramref name="targetSnapshot"/>;
/// source database FKs do not affect execution order.
/// </para>
/// </remarks>
public interface IDependencyResolver
{
    /// <summary>
    /// Builds an execution plan that respects FK dependencies among the selected target tables.
    /// </summary>
    /// <param name="targetSnapshot">
    /// The target database schema snapshot containing FK metadata.
    /// </param>
    /// <param name="selectedTargetTables">
    /// Tables selected for this sync job (from enabled <c>TableTask</c> entries).
    /// Must match tables present in <paramref name="targetSnapshot"/>.
    /// </param>
    /// <returns>
    /// Success: <see cref="ExecutionPlan"/> with topologically sorted insert/delete orders.
    /// Failure: Error message with details about missing parents or cycles.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="targetSnapshot"/> or <paramref name="selectedTargetTables"/> is null.
    /// </exception>
    /// <remarks>
    /// <para><b>Failure Scenarios:</b></para>
    /// <list type="bullet">
    /// <item>
    /// <description>A selected table has a FK to a non-selected target table.</description>
    /// </item>
    /// <item>
    /// <description>A cycle exists among selected tables (e.g., A → B → C → A).</description>
    /// </item>
    /// </list>
    /// <para><b>Success Behavior:</b></para>
    /// <list type="bullet">
    /// <item>
    /// <description>InsertOrder places parent tables before children.</description>
    /// </item>
    /// <item>
    /// <description>DeleteOrder is the reverse (children before parents).</description>
    /// </item>
    /// <item>
    /// <description>Tables with no FKs may appear in any consistent order.</description>
    /// </item>
    /// </list>
    /// </remarks>
    Result<ExecutionPlan> BuildExecutionPlan(
        SchemaSnapshot targetSnapshot,
        IReadOnlyList<TableIdentifier> selectedTargetTables);
}
```

**Validation Checklist:**
- [ ] Single method signature matches spec
- [ ] Returns `Result<ExecutionPlan>` from `DotNetToolkit.General`
- [ ] XML documentation explains both success and failure paths
- [ ] Remarks clarify dependency closure + cycle detection rules
- [ ] Notes that only Target FKs matter

---

### **Step 3: Implement Dependency Resolver**

#### 3.1 Create `DependencyResolver.cs`

**File:** `ReportSyncer.Core/Schema/Dependency/DependencyResolver.cs`

**Implementation Strategy:**
1. Build adjacency list (child → parents)
2. Validate dependency closure (all parents present)
3. Perform topological sort with cycle detection (DFS-based or Kahn's algorithm)
4. Generate insert order, then reverse for delete order

**Code Template (Outline):**
```csharp
// File: ReportSyncer.Core/Schema/Dependency/DependencyResolver.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using DotNetToolkit.General;

namespace ReportSyncer.Core.Schema.Dependency;

/// <summary>
/// Default implementation of <see cref="IDependencyResolver"/> using topological sorting.
/// </summary>
public sealed class DependencyResolver : IDependencyResolver
{
    public Result<ExecutionPlan> BuildExecutionPlan(
        SchemaSnapshot targetSnapshot,
        IReadOnlyList<TableIdentifier> selectedTargetTables)
    {
        Guard.NotNull(targetSnapshot, nameof(targetSnapshot));
        Guard.NotNull(selectedTargetTables, nameof(selectedTargetTables));

        // Step 1: Build selected table set
        var selectedSet = new HashSet<TableIdentifier>(selectedTargetTables);
        
        // Step 2: Build adjacency list and validate closure
        var adjList = new Dictionary<TableIdentifier, List<TableIdentifier>>();
        var errors = new List<DependencyValidationError>();

        foreach (var table in selectedTargetTables)
        {
            if (!targetSnapshot.Tables.TryGetValue(table, out var tableSchema))
            {
                // Table not found in snapshot - this is a schema inspection problem,
                // not a dependency problem. Could throw or return error.
                continue;
            }

            adjList[table] = new List<TableIdentifier>();

            foreach (var fk in tableSchema.ForeignKeys)
            {
                // FK points to parent table
                if (!selectedSet.Contains(fk.ToTable))
                {
                    errors.Add(DependencyValidationError.MissingParent(table, fk.ToTable));
                }
                else
                {
                    adjList[table].Add(fk.ToTable);
                }
            }
        }

        // Step 3: Return early if closure validation failed
        if (errors.Count > 0)
        {
            var errorMsg = string.Join("; ", errors.Select(e => e.Message));
            return Result<ExecutionPlan>.Fail(errorMsg);
        }

        // Step 4: Topological sort with cycle detection
        var sortResult = TopologicalSort(adjList, selectedSet);
        if (!sortResult.IsSuccess)
        {
            return Result<ExecutionPlan>.Fail(sortResult.Error!);
        }

        var insertOrder = sortResult.Value;
        var deleteOrder = insertOrder.Reverse().ToArray();

        return Result<ExecutionPlan>.Ok(new ExecutionPlan(insertOrder, deleteOrder));
    }

    /// <summary>
    /// Performs DFS-based topological sort.
    /// </summary>
    private Result<IReadOnlyList<TableIdentifier>> TopologicalSort(
        Dictionary<TableIdentifier, List<TableIdentifier>> adjList,
        HashSet<TableIdentifier> allNodes)
    {
        var visited = new HashSet<TableIdentifier>();
        var visiting = new HashSet<TableIdentifier>();
        var result = new List<TableIdentifier>();

        foreach (var node in allNodes)
        {
            if (!visited.Contains(node))
            {
                var visitResult = Visit(node, adjList, visited, visiting, result);
                if (!visitResult.IsSuccess)
                {
                    return Result<IReadOnlyList<TableIdentifier>>.Fail(visitResult.Error!);
                }
            }
        }

        return Result<IReadOnlyList<TableIdentifier>>.Ok(result);
    }

    /// <summary>
    /// DFS visit with cycle detection.
    /// </summary>
    private Result<bool> Visit(
        TableIdentifier node,
        Dictionary<TableIdentifier, List<TableIdentifier>> adjList,
        HashSet<TableIdentifier> visited,
        HashSet<TableIdentifier> visiting,
        List<TableIdentifier> result)
    {
        if (visiting.Contains(node))
        {
            // Cycle detected
            return Result<bool>.Fail($"Cycle detected involving table '{node}'.");
        }

        if (visited.Contains(node))
        {
            return Result<bool>.Ok(true);
        }

        visiting.Add(node);

        if (adjList.TryGetValue(node, out var dependencies))
        {
            foreach (var dep in dependencies)
            {
                var visitResult = Visit(dep, adjList, visited, visiting, result);
                if (!visitResult.IsSuccess)
                {
                    return visitResult;
                }
            }
        }

        visiting.Remove(node);
        visited.Add(node);
        result.Add(node);

        return Result<bool>.Ok(true);
    }
}
```

**Implementation Notes:**
- Use DFS for topological sort (standard algorithm)
- `visiting` set detects cycles during DFS
- Insert order = post-order DFS result
- Delete order = reverse of insert order

**Validation Checklist:**
- [ ] Constructor has no dependencies (stateless implementation)
- [ ] Guard clauses validate inputs
- [ ] Builds adjacency list from FK relationships
- [ ] Detects missing parent tables before sorting
- [ ] DFS-based topological sort with cycle detection
- [ ] Returns `Result<ExecutionPlan>` with clear error messages
- [ ] Delete order is reverse of insert order

---

### **Step 4: Unit Tests**

#### 4.1 Create `DependencyResolverUnitTests.cs`

**File:** `Tests/ReportSyncer.Core.Tests/UnitTests/Schema/Dependency/DependencyResolverUnitTests.cs`

**Test Coverage Requirements:**

| Test Case | Purpose | Expected Result |
|-----------|---------|----------------|
| `BuildExecutionPlan_WithNoForeignKeys_ReturnsAllTablesInAnyOrder` | Baseline: no dependencies | Success, all tables present in both orders |
| `BuildExecutionPlan_WithSimpleParentChild_ReturnsParentBeforeChild` | FK: Orders → Customers | InsertOrder = [Customers, Orders] |
| `BuildExecutionPlan_WithChainDependency_ReturnsCorrectOrder` | A → B → C | InsertOrder = [C, B, A] |
| `BuildExecutionPlan_WithMissingParent_FailsWithClearError` | Selected: Orders; Not selected: Customers (but Orders → Customers) | Failure listing Orders and missing Customers |
| `BuildExecutionPlan_WithCycle_FailsWithCycleError` | A → B → A | Failure mentioning cycle |
| `BuildExecutionPlan_WithMultipleMissingParents_ListsAllErrors` | Multiple tables missing dependencies | Error message lists all missing tables |
| `BuildExecutionPlan_DeleteOrder_IsReverseOfInsertOrder` | Any valid graph | DeleteOrder == InsertOrder.Reverse() |

**Code Template (Sample Tests):**
```csharp
// File: Tests/ReportSyncer.Core.Tests/UnitTests/Schema/Dependency/DependencyResolverUnitTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using FluentAssertions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using Xunit;

namespace ReportSyncer.Core.Tests.UnitTests.Schema.Dependency;

public class DependencyResolverUnitTests
{
    private readonly IDependencyResolver _resolver = new DependencyResolver();

    [Fact]
    public void BuildExecutionPlan_WithNoForeignKeys_ReturnsAllTablesInAnyOrder()
    {
        // Arrange
        var table1 = new TableIdentifier("dbo", "Table1");
        var table2 = new TableIdentifier("dbo", "Table2");
        
        var snapshot = CreateSnapshot(
            (table1, Array.Empty<ForeignKeySchema>()),
            (table2, Array.Empty<ForeignKeySchema>())
        );
        
        var selected = new[] { table1, table2 };

        // Act
        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InsertOrder.Should().HaveCount(2);
        result.Value.InsertOrder.Should().Contain(table1);
        result.Value.InsertOrder.Should().Contain(table2);
        result.Value.DeleteOrder.Should().Equal(result.Value.InsertOrder.Reverse());
    }

    [Fact]
    public void BuildExecutionPlan_WithSimpleParentChild_ReturnsParentBeforeChild()
    {
        // Arrange: Orders → Customers
        var customers = new TableIdentifier("dbo", "Customers");
        var orders = new TableIdentifier("dbo", "Orders");
        
        var fkOrdersToCustomers = new ForeignKeySchema(
            "FK_Orders_Customers",
            fromTable: orders,
            toTable: customers,
            columnPairs: new[] { new ColumnPair("CustomerId", "CustomerId") },
            isCascadeDelete: false
        );

        var snapshot = CreateSnapshot(
            (customers, Array.Empty<ForeignKeySchema>()),
            (orders, new[] { fkOrdersToCustomers })
        );
        
        var selected = new[] { customers, orders };

        // Act
        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InsertOrder.Should().Equal(customers, orders);
        result.Value.DeleteOrder.Should().Equal(orders, customers);
    }

    [Fact]
    public void BuildExecutionPlan_WithMissingParent_FailsWithClearError()
    {
        // Arrange: Orders → Customers, but only Orders is selected
        var customers = new TableIdentifier("dbo", "Customers");
        var orders = new TableIdentifier("dbo", "Orders");
        
        var fk = new ForeignKeySchema(
            "FK_Orders_Customers",
            fromTable: orders,
            toTable: customers,
            columnPairs: new[] { new ColumnPair("CustomerId", "CustomerId") },
            isCascadeDelete: false
        );

        var snapshot = CreateSnapshot(
            (customers, Array.Empty<ForeignKeySchema>()),
            (orders, new[] { fk })
        );
        
        var selected = new[] { orders }; // Missing customers!

        // Act
        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Orders");
        result.Error.Should().Contain("Customers");
        result.Error.Should().Contain("not included");
    }

    [Fact]
    public void BuildExecutionPlan_WithCycle_FailsWithCycleError()
    {
        // Arrange: A → B → A
        var tableA = new TableIdentifier("dbo", "TableA");
        var tableB = new TableIdentifier("dbo", "TableB");
        
        var fkAtoB = new ForeignKeySchema("FK_A_B", tableA, tableB,
            new[] { new ColumnPair("BId", "Id") }, false);
        var fkBtoA = new ForeignKeySchema("FK_B_A", tableB, tableA,
            new[] { new ColumnPair("AId", "Id") }, false);

        var snapshot = CreateSnapshot(
            (tableA, new[] { fkAtoB }),
            (tableB, new[] { fkBtoA })
        );
        
        var selected = new[] { tableA, tableB };

        // Act
        var result = _resolver.BuildExecutionPlan(snapshot, selected);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Cycle");
    }

    // Helper to create SchemaSnapshot
    private static SchemaSnapshot CreateSnapshot(
        params (TableIdentifier table, ForeignKeySchema[] fks)[] tables)
    {
        var tableSchemas = new Dictionary<TableIdentifier, TableSchema>();
        
        foreach (var (table, fks) in tables)
        {
            var schema = new TableSchema(
                table,
                Array.Empty<ColumnSchema>(),
                Array.Empty<string>(),
                fks
            );
            tableSchemas[table] = schema;
        }

        return new SchemaSnapshot(tableSchemas, SchemaRole.Target);
    }
}
```

**Validation Checklist:**
- [ ] All 7 core test cases implemented
- [ ] Tests use `FluentAssertions` for readable assertions
- [ ] Helper method creates test snapshots easily
- [ ] Tests verify both success and failure paths
- [ ] Error messages validated for clarity

---

### **Step 5: Integration & Verification**

#### 5.1 Compile and Test

**Commands:**
```powershell
# From ReportSyncer root
dotnet build

# Run dependency resolver tests only
dotnet test --filter "FullyQualifiedName~DependencyResolverUnitTests"

# Run all schema tests
dotnet test --filter "FullyQualifiedName~Schema"
```

**Expected Results:**
- ✅ Zero compilation errors
- ✅ All tests pass
- ✅ Code coverage for happy path + error paths

#### 5.2 Manual Verification

Create a small integration test (optional but recommended):

```csharp
[Fact]
public void IntegrationTest_WithRealSchemaModels_ProducesCorrectOrder()
{
    // Use TableSchema, ForeignKeySchema, SchemaSnapshot as-is
    // Verify realistic multi-table scenario (e.g., 5 tables with mixed dependencies)
}
```

---

## 🔗 Dependencies & References

### Depends On (Must Exist First):
- ✅ `ReportSyncer.Core.Schema.TableIdentifier`
- ✅ `ReportSyncer.Core.Schema.TableSchema`
- ✅ `ReportSyncer.Core.Schema.ForeignKeySchema`
- ✅ `ReportSyncer.Core.Schema.SchemaSnapshot`
- ✅ `DotNetToolkit.General.Result<T>`
- ✅ `DotNetToolkit.General.Guard`

### Required By (Blocks):
- ⏳ Task 4.1: `ISchemaService` (will consume `IDependencyResolver`)
- ⏳ Task 4.3: `PreFlightValidator` (will use execution plan)
- ⏳ Section 5: Sync execution (will follow `ExecutionPlan` order)

---

## ⚠️ Common Pitfalls & Warnings

### ❌ Don't:
1. **Don't use Source FKs for ordering** — only Target FKs matter
2. **Don't silently ignore missing parents** — must fail explicitly
3. **Don't implement execution logic here** — this is planning only
4. **Don't add safety checks** — those belong in a separate validator
5. **Don't optimize prematurely** — topological sort is fast enough for hundreds of tables

### ✅ Do:
1. **Do validate inputs** — null checks, empty collections
2. **Do provide detailed error messages** — include table names
3. **Do keep it stateless** — no instance fields beyond interface dependencies
4. **Do test edge cases** — self-referencing FKs, disconnected components
5. **Do document algorithm choice** — DFS vs Kahn's, why chosen

---

## 📊 Success Criteria

### Code Quality:
- [ ] All files have proper headers (author, date, project)
- [ ] XML documentation on all public types/methods
- [ ] Guard clauses on public method inputs
- [ ] No hardcoded strings in error paths (use constants or factory methods)

### Functionality:
- [ ] Handles empty table list (returns empty plan)
- [ ] Handles no FK scenario (any order is valid)
- [ ] Detects all missing parent tables
- [ ] Detects cycles with clear error
- [ ] Produces correct topological order for complex graphs

### Testing:
- [ ] Minimum 7 unit tests covering matrix above
- [ ] FluentAssertions used for readable test assertions
- [ ] Tests run green in CI
- [ ] No test warnings or skipped tests

### Integration:
- [ ] Compiles without errors
- [ ] No new warnings introduced
- [ ] Follows existing project conventions (namespaces, file structure)
- [ ] Ready for Task 4.1 (`ISchemaService`) to consume

---

## 🎓 Algorithm Reference

### Topological Sort (DFS-Based)

**Pseudocode:**
```
function TopologicalSort(adjList, nodes):
    visited = {}
    visiting = {}
    result = []
    
    for each node in nodes:
        if node not in visited:
            Visit(node, adjList, visited, visiting, result)
    
    return result

function Visit(node, adjList, visited, visiting, result):
    if node in visiting:
        throw CycleDetected
    if node in visited:
        return
    
    visiting.add(node)
    
    for each dependency in adjList[node]:
        Visit(dependency, adjList, visited, visiting, result)
    
    visiting.remove(node)
    visited.add(node)
    result.add(node)
```

**Key Properties:**
- Time Complexity: O(V + E) where V = tables, E = FK relationships
- Space Complexity: O(V)
- Detects cycles during traversal
- Post-order visit produces topological ordering

---

## 📚 Additional Context

### Why Dependency Closure Matters (from PRD)

> "If any selected table has a foreign key to another Target table that is not selected for the job, pre-flight must fail with a clear error listing the missing tables."

**Rationale:**
- Prevents orphaned records (inserting Orders without Customers)
- Ensures referential integrity maintained
- Forces user to make explicit decisions about data scope

### Why Only Target FKs (from Architecture)

> "Only use **Target FK graph** for dependency (Source FK not参与排序)."

**Rationale:**
- Source is read-only (never modified)
- Insert order matters for Target constraints, not Source
- Simplifies implementation and error messages

---

## ✅ Final Checklist for AI Coder

Before marking Task 4.0 complete:

### Code:
- [ ] `ExecutionPlan.cs` created with XML docs
- [ ] `DependencyValidationError.cs` with factory methods
- [ ] `IDependencyResolver.cs` interface with full docs
- [ ] `DependencyResolver.cs` implementation with DFS sort
- [ ] All files have proper headers

### Tests:
- [ ] `DependencyResolverUnitTests.cs` with 7+ test cases
- [ ] Helper methods for creating test snapshots
- [ ] All tests pass locally

### Verification:
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes all new tests
- [ ] No new compiler warnings
- [ ] Code follows project style (file headers, naming)

### Documentation:
- [ ] This playbook updated if implementation deviates
- [ ] Any algorithm changes documented in code comments
- [ ] Edge cases noted in XML remarks

---

## 🔗 Next Steps

After Task 4.0 completion:
1. **Task 4.1:** Implement `ISchemaService` / `SchemaService` (will call `IDependencyResolver`)
2. **Task 4.2:** Define Sync contracts (`ISyncOrchestrator`, `PreFlightResult`, etc.)
3. **Task 4.3:** Implement `PreFlightValidator` (will use `ExecutionPlan`)

---

**End of Playbook**
