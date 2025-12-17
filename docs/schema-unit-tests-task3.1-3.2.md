# Schema Unit Test Instructions (Task 3.1 & Task 3.2)

Audience: AI coder adding **unit tests only** (no integration/real DB) for ReportSyncer.Core schema work.

## Scope & Constraints
- Project: `ReportSyncer.Tests` (follow existing folder/namespace patterns, e.g., `Schema`).
- Type: Unit tests only; no real SQL or LocalDB. Use mocks/stubs for DB abstractions.
- Frameworks: xUnit, FluentAssertions, Moq (MockBehavior.Strict). Follow AAA.
- Traits: add `[Trait("Type","Unit")]` and optionally `[Trait("Area","Schema")]`.
- Keep tests deterministic/parallel-safe; no ambient time/file system dependencies.

## Task 3.1 – Schema Domain Model Unit Tests
Target types: `TableIdentifier`, `ColumnSchema`, `ColumnPair`, `ForeignKeySchema`, `TableSchema`, `SchemaSnapshot`.

Test cases to cover:
1) `TableIdentifier`
   - Ctor rejects blank schema/table.
   - Equality/hash are case-insensitive for schema+table (dbo.Customer equals DBO.CUSTOMER); differing schemas are not equal.
   - `ToString()` formats as `schema.table`.
2) `ColumnSchema`
   - Ctor rejects blank name.
   - Null `ClrType` defaults to `typeof(object)`.
   - Blank `DbType` becomes empty string.
   - Flags (`IsNullable`, `IsIdentity`, `IsPrimaryKeyPart`, `MaxLength`) are preserved.
3) `ColumnPair`
   - Ctor rejects blank `FromColumn`/`ToColumn`.
4) `ForeignKeySchema`
   - Ctor rejects blank name, null tables, or null/empty `ColumnPairs`.
   - Preserves cascade flag and column pair order.
5) `TableSchema`
   - Ctor rejects null `Table`.
   - Null columns/PKs/FKs default to empty collections.
   - `GetColumn` is case-insensitive and returns null for blank/missing names.
6) `SchemaSnapshot`
   - Ctor rejects null tables collection.
   - Case-insensitive lookup succeeds; `GetRequiredTable` throws `KeyNotFoundException` for missing table with message containing the table id.
   - `Tables` exposes a copy (mutating returned list does not affect internal state).

## Task 3.2 – Schema Inspection Unit Tests (SqlServerSchemaInspector)
Target: `SqlServerSchemaInspector` behavior using mocked `IDbContext`/`IDbCommandWrapper`.

Test cases to cover:
1) Guards
   - `InspectAsync` throws `ArgumentNullException` when `connection` is null.
   - `InspectAsync` throws when `tables` is null.
   - Empty requested tables returns empty `SchemaSnapshot` with Role = Target.
2) Missing table
   - When column query returns empty for a requested table, throws `SchemaMismatchException` containing the table name.
3) Column mapping
   - Column rows map to `ColumnSchema` with correct `ClrType`, `DbType`, nullability, identity, PK flags, and `MaxLength` handling (0/null -> null).
4) PK collection
   - Primary key columns aggregated from column rows match expected names.
5) FK grouping & cascade
   - FK rows grouped by name produce `ForeignKeySchema` with correct from/to tables and ordered column pairs; cascade flag set when delete action is "CASCADE" (case-insensitive).
6) FK attachment scope
   - FKs attach only to requested tables (ignore others); case-insensitive table matching.
7) Type mapping fallback
   - Unknown/empty SQL types map to `typeof(object)`.
8) Exception wrapping
   - Unexpected exception from `IDbContext` is wrapped in `SyncExecutionException` with message preserved.

## Test Structure & Setup Notes
- Place tests under `Schema` folder/namespace (e.g., `ReportSyncer.Tests.Schema`).
- Use minimal DTOs/builders for row projections used by `SqlServerSchemaInspector` to simulate query results.
- For mocks, set up `IDbContext.CreateCommand(...)`, parameter adds, and `ExecuteQueryAsync<T>` returns per test needs; verify key calls when meaningful.
- Prefer helper methods to build `TableIdentifier`, column rows, FK rows to keep Arrange sections concise.

## Out of Scope
- No integration tests here (no real SQL Server/LocalDB). Those belong in separate integration suites.
- No changes to production code unless a test reveals a clear bug and is explicitly approved.
