**1. Overview**  
Integration test instructions for Schema tasks 3.1/3.2 (Table/Column/ForeignKey/TableSchema/SchemaSnapshot + SqlServerSchemaInspector) using real SQL Server/LocalDB. Aligned with existing unit-test style.

**2. Scope & Constraints**  
- Real DB required for schema reads: use actual SQL Server/LocalDB via `Microsoft.Data.SqlClient`, DotNetToolkit.Database abstractions, and the real `SqlServerSchemaInspector`.  
- No production code changes unless a test exposes a bug; only minimal fix with accompanying test.  
- Deterministic, repeatable, isolated: each test creates a unique temp DB/schema, cleans up afterward; no cross-test coupling.  
- Parallelism: use collection fixture to serialize DB-using tests within the collection; other test collections may run in parallel.  
- Traits: `[Trait("Type","IntegrationTest")]` and `[Trait("Area","Schema")]`.  
- Dependencies: no new packages; use repo-existing xUnit/FluentAssertions/Moq (if needed for helpers only) and `Microsoft.Data.SqlClient`.  
- Mocks only for peripheral helpers (e.g., logging) if unavoidable; schema retrieval must hit real DB.

**3. Folder / Namespace / Traits**  
- Unit tests root: `Tests/ReportSyncer.Core.Tests/UnitTests/...` (existing).  
- Integration tests root: Schema.  
- Namespace: `ReportSyncer.Core.Tests.IntegrationTests.Schema`.  
- File naming: one test class per file, named `XxxIntegrationTests.cs` (unit counterparts remain `XxxUnitTests.cs`).  
- Traits on class (preferred) or methods: `[Trait("Type","IntegrationTest")]`, `[Trait("Area","Schema")]`.

**4. Test Environment & Config**  
- Connection string from env var `REPORTSYNCER_TEST_SQL_CONN` (points to a server where you can create/drop temp DBs).  
- If env var missing/empty: **skip** tests (not fail) with a clear skip message guiding setup.  
- Allowed targets: preferred `(localdb)\MSSQLLocalDB` or `Server=.;` with Integrated Security; configurable SQL Server via env/CI secret also acceptable. No Docker/Testcontainers unless already present (they are not).  
- Use `master` connection to create/drop unique temp DBs per test run.

**5. Shared Test Infrastructure (Reusable)**  
- Add a fixture `SchemaIntegrationDatabaseFixture` (in `IntegrationTests/Schema/`) implementing `IAsyncLifetime`, used via `[Collection("SchemaIntegrationTests")]` to serialize DB usage. Responsibilities:  
  - Read `REPORTSYNCER_TEST_SQL_CONN`; if missing, set `SkipReason` for consumers.  
  - Create unique DB name per test class/run (e.g., `ReportSyncer_Schema_IT_{Guid:N}`); create/drop DB in `InitializeAsync`/`DisposeAsync`.  
  - Provide helper `ExecuteNonQueryAsync(string sql)` for DDL/DML, and `CreateDbContextFactory(DatabaseSettings settings)` returning `Func<ConnectionConfig, IDbContext>` using `DbConnectionFactory`/`DbContext`.  
- Optional helper `SqlScriptRunner` for executing batches of DDL strings.  
- Parallelism: mark collection to disable parallelization within the collection; unique DB name per fixture to avoid conflicts.  
- Example fixture skeleton (keep brief):  
```csharp
[CollectionDefinition("SchemaIntegrationTests", DisableParallelization = true)]
public sealed class SchemaIntegrationCollection : ICollectionFixture<SchemaIntegrationDatabaseFixture> { }

public sealed class SchemaIntegrationDatabaseFixture : IAsyncLifetime
{
    public string? SkipReason { get; private set; }
    public string? ConnectionString { get; private set; }
    public string? DatabaseName { get; private set; }

    public async Task InitializeAsync()
    {
        var baseConn = Environment.GetEnvironmentVariable("REPORTSYNCER_TEST_SQL_CONN");
        if (string.IsNullOrWhiteSpace(baseConn))
        {
            SkipReason = "Set REPORTSYNCER_TEST_SQL_CONN to run schema integration tests.";
            return;
        }

        DatabaseName = $"ReportSyncer_Schema_IT_{Guid.NewGuid():N}";
        var masterConn = $"{baseConn};Initial Catalog=master";
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();
        await using var create = new SqlCommand($"CREATE DATABASE [{DatabaseName}];", conn);
        await create.ExecuteNonQueryAsync();
        ConnectionString = $"{baseConn};Initial Catalog={DatabaseName}";
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(DatabaseName)) return;
        var masterConn = $"{ConnectionString?.Replace($"Initial Catalog={DatabaseName}", "Initial Catalog=master")}";
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();
        await using var drop = new SqlCommand(
            $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}];", conn);
        await drop.ExecuteNonQueryAsync();
    }

    public async Task ExecuteNonQueryAsync(string sql)
    {
        if (ConnectionString is null) throw new InvalidOperationException("Fixture not initialized.");
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public Func<ConnectionConfig, IDbContext> CreateDbContextFactory()
    {
        if (ConnectionString is null) throw new InvalidOperationException("Fixture not initialized.");
        var settings = Options.Create(new DatabaseSettings
        {
            ProviderName = "Microsoft.Data.SqlClient",
            ConnectionString = ConnectionString,
            CommandTimeoutSeconds = 30
        });
        var factory = new DbConnectionFactory(settings);
        var sp = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IDbConnectionFactory>(factory)
            .AddSingleton<IDataMapper<object>, ReflectionDataMapper<object>>() // minimal mapper
            .BuildServiceProvider();
        return _ => new DbContext(factory, settings, sp.GetRequiredService<ILogger<DbContext>>(), sp);
    }
}
```
(Keep actual mapper registrations minimal; adjust generic mapper as needed.)

**6. Integration Test Tasks & Steps**  
_All tests live in `IntegrationTests/Schema`, use the collection above, and skip when `SkipReason` is set._

- **Class**: `SqlServerSchemaInspectorIntegrationTests.cs`  
  - **Test**: `InspectAsync_WithEmptyTables_ReturnsEmptySnapshotWithSourceRole`  
    - Arrange: Fixture initialized; call inspector with empty table list.  
    - Act: `InspectAsync(connectionConfig, Enumerable.Empty<TableIdentifier>(), ct)`.  
    - Assert: Snapshot has Role=Source, `Tables` empty, no DB objects required.  
  - **Test**: `InspectAsync_WithSingleTable_ReturnsColumnsPkAndTypes`  
    - Arrange: Create table `dbo.Customers` with identity PK, nullable/non-nullable columns, varying max lengths (`varchar(50)`, `nvarchar(max)`, `int`).  
    - Act: Inspect `dbo.Customers`.  
    - Assert: One `TableSchema`; columns include correct `DbType`, `ClrType` mapping, `IsNullable`, `IsIdentity`, `IsPrimaryKeyPart`, `MaxLength` (null for max/0); PK list matches.  
  - **Test**: `InspectAsync_WithForeignKeyCascade_ReturnsFkWithPairsAndCascadeFlag`  
    - Arrange: Create `dbo.Customers(Id PK)` and `dbo.Orders(Id PK, CustomerId FK references Customers(Id) ON DELETE CASCADE)`.  
    - Act: Inspect both tables.  
    - Assert: FK present on `Orders` with correct from/to tables, column pairs order preserved, `IsCascadeDelete` true.  
  - **Test**: `InspectAsync_WithMultiColumnForeignKey_ReturnsOrderedPairs`  
    - Arrange: Parent `dbo.Parents(P1,P2 PK)`, child `dbo.Children(C1,C2, P1,P2 FK references Parents(P1,P2))`.  
    - Act/Assert: FK has two `ColumnPair` entries in constraint order; PK list correct.  
  - **Test**: `InspectAsync_WithMissingTable_ThrowsSchemaMismatchException`  
    - Arrange: No such table; request `dbo.Nope`.  
    - Act/Assert: Throws `SchemaMismatchException` with message containing `dbo.Nope`.  
  - **Test**: `InspectAsync_WithUnknownType_MapsToObject`  
    - Arrange: Create table with column `Weird sql_variant` (or user-defined type);  
    - Act/Assert: Column `ClrType` is `typeof(object)`; `DbType` matches reported name.  
  - **Test**: `InspectAsync_WithBadConnection_WrapsInSyncExecutionException`  
    - Arrange: Use intentionally bad connection string (e.g., wrong server name) but valid `ConnectionConfig`.  
    - Act/Assert: Throws `SyncExecutionException` with original message contained.

- **Class**: `SchemaSnapshotIntegrationTests.cs` (optional if behavior differs only with live data; otherwise keep assertions inside inspector tests)  
  - Focus on ensuring `Tables` copy semantics if retrieved from inspector result and mutated locally (does not affect original snapshot).

**Arrange/DDL snippets (use helper to execute)**  
- Single table:
```sql
CREATE TABLE dbo.Customers(
  Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
  Name VARCHAR(50) NOT NULL,
  Notes NVARCHAR(MAX) NULL,
  Age INT NULL
);
```
- FK cascade:
```sql
CREATE TABLE dbo.Orders(
  Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
  CustomerId INT NOT NULL,
  CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId)
    REFERENCES dbo.Customers(Id) ON DELETE CASCADE
);
```
- Multi-column FK:
```sql
CREATE TABLE dbo.Parents(
  P1 INT NOT NULL,
  P2 INT NOT NULL,
  CONSTRAINT PK_Parents PRIMARY KEY (P1, P2)
);
CREATE TABLE dbo.Children(
  C1 INT NOT NULL,
  C2 INT NOT NULL,
  P1 INT NOT NULL,
  P2 INT NOT NULL,
  CONSTRAINT PK_Children PRIMARY KEY (C1, C2),
  CONSTRAINT FK_Children_Parents FOREIGN KEY (P1, P2)
    REFERENCES dbo.Parents(P1, P2)
);
```
- Weird type:
```sql
CREATE TABLE dbo.WeirdTypes(
  Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
  Payload sql_variant NULL
);
```

**Assert focus**  
- Verify `SchemaSnapshot.Role`, table count, column count.  
- Per column: `Name`, `DbType`, `ClrType`, `IsNullable`, `IsIdentity`, `IsPrimaryKeyPart`, `MaxLength`.  
- PK list equality (case-insensitive).  
- FK: `Name`, `FromTable`, `ToTable`, ordered `ColumnPairs`, `IsCascadeDelete`.  
- Exceptions: type and message substring with table name or connection error.

**7. Deliverables Checklist**  
- New/updated files:  
  - docs (if not already present): ensure `docs/schema-integration-tests-task3.1-3.2.md` reflects this instruction.  
  - Tests:  
    - `Tests/ReportSyncer.Core.Tests/IntegrationTests/Schema/SqlServerSchemaInspectorIntegrationTests.cs`  
    - (Optional) `Tests/ReportSyncer.Core.Tests/IntegrationTests/Schema/SchemaSnapshotIntegrationTests.cs`  
    - `Tests/ReportSyncer.Core.Tests/IntegrationTests/Schema/SchemaIntegrationDatabaseFixture.cs` (and collection definition)  
    - Any helper/DDL script directory if chosen: `Tests/ReportSyncer.Core.Tests/TestFiles/SchemaIntegration/`  
- Ensure ReportSyncer.Core.Tests.csproj already includes `IntegrationTests/Schema` folder (it does); no package changes.

**8. Definition of Done**  
- `dotnet test` passes when `REPORTSYNCER_TEST_SQL_CONN` is provided.  
- When env var is missing, schema integration tests are skipped with a clear message (not failed).  
- Tests leave no residue: temp DBs/schemas dropped even on failure.  
- Assertions cover schema fidelity (types/nullability/identity/max length, PK/FK ordering, cascade flags) and error wrapping.  
- Test output/logging minimal but sufficient to diagnose failures (e.g., include DB name on failure).  
- No changes to production code unless a defect is found and fixed with accompanying tests.