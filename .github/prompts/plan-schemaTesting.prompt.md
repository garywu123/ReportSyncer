## Plan: Schema Testing Cases

Draft test matrix for ReportSyncer.Core Schema to cover value objects and inspector behavior.

## Code requirements (tests)
1. Use xUnit for all tests. Follow AAA (Arrange/Act/Assert) structure.
2. Use FluentAssertions for assertions (including exception assertions with message when meaningful).
3. Use Moq for mocking external dependencies. Prefer MockBehavior.Strict for key collaborators.
   - Only mock boundaries (IO/clock/network/db/services). Do not mock simple DTOs/value objects.
   - Verify important interactions (calls, times, args) explicitly.
4. Keep tests deterministic and isolated:
   - No dependency on current time/timezone/random unless controlled (inject clock, fixed seed).
   - No real user file paths. Use temp directories and clean up.
   - Unit tests must be parallel-safe; integration tests must declare [Collection] if shared state exists.
5. Reuse test setup/data:
   - Use xUnit fixtures (IClassFixture / ICollectionFixture) for shared expensive setup.
   - For parameterized cases use [Theory] + InlineData for primitives; MemberData/ClassData/Builders for complex objects.
6. Categorize tests and enforce execution:
   - Add [Trait("Type","Unit")] or [Trait("Type","Integration")] and optionally [Trait("Area","Schema")] etc.
   - Maintain folder/namespace conventions consistent with ReportSyncer.Tests structure.
7. Coverage expectations:
   - For each public method: cover happy path + key edge cases + error/exception paths.


### Steps
1. Catalog unit cases for value objects: validate guards/equality in [Schema/ColumnSchema.cs](ReportSyncer.Core/Schema/ColumnSchema.cs), [ForeignKeySchema.cs](ReportSyncer.Core/Schema/ForeignKeySchema.cs), [TableIdentifier.cs](ReportSyncer.Core/Schema/TableIdentifier.cs), [TableSchema.cs](ReportSyncer.Core/Schema/TableSchema.cs), [SchemaSnapshot.cs](ReportSyncer.Core/Schema/SchemaSnapshot.cs).
2. Define unit cases for mapper helpers in [SqlServerSchemaInspector.cs](ReportSyncer.Core/Schema/SqlServerSchemaInspector.cs) using fakes: null/empty inputs, type mapping, FK grouping, cascade flag, TableIdentifier comparer behavior.
3. Separate integration scenarios for SqlServerSchemaInspector with real IDbContext: actual column load, PK/identity/nullable flags, FK attachment to parents, case-insensitive lookups, error wrapping.

### Further Considerations
1. Confirm whether to mock IDbContext for inspector unit tests vs lightweight in-memory stub (no real SQL).
2. Decide target DB for integration (LocalDB vs container SQL) and test fixture lifecycle.
