

# Testing Instruction (C# / xUnit)

These rules define how to write, organize, and run tests in this repository. The goal is fast feedback, high signal, deterministic behavior, and clear separation between pure unit tests and environment-coupled integration tests.
## 1) Frameworks and style

1. Use **xUnit** for all tests.
2. Follow **AAA** (Arrange / Act / Assert) structure.
3. Use **FluentAssertions** for assertions:

   * Use exception assertions when appropriate.
   * Include message checks only when they are stable and meaningful (avoid brittle string checks).
4. Use **Moq** for mocking:

   * Prefer `MockBehavior.Strict` for key collaborators.
   * Only mock **boundaries** (IO, clock, network, DB, external services).
   * Do **not** mock simple DTOs/value objects.
   * Verify important interactions explicitly (call count, arguments).

## 2) Test project, folder, namespace conventions (must follow)

### Preferred layout (recommended)

* Keep unit tests and integration tests in **separate test projects**, for example, 

```
/tests
  /ReportSyncer.Core.UnitTests
  /ReportSyncer.Core.IntegrationTests
```

### Acceptable layout (if only one test project exists)

* Use **top-level folders** and matching namespaces:

```
/tests/ReportSyncer.Core.Tests
  /Unit/**
  /Integration/**
```

### Namespace rules

* Namespaces **mirror** folder structure.
* Examples:
  * `ReportSyncer.UnitTests.Schema` or `ReportSyncer.Tests.Unit.Schema`
  * `ReportSyncer.IntegrationTests.Schema` or `ReportSyncer.Tests.Integration.Schema`

### Class and file naming
* File name matches the class name.
* Unit test classes: `{SubjectUnderTest}Tests`
* Integration test classes: `{SubjectUnderTest}IntegrationTests`
* Do **not** mix Unit and Integration tests in the same test class.

## 3) Categorization (Trait) rules

1. Put stable traits at the **class** level:
   * `[Trait("Type","UnitTest")]` or `[Trait("Type","IntegrationTest")]`
   * `[Trait("Area","Schema")]` etc.
1. Put exception/special-case traits at the **method** level only:
   * `Bug`, `Scenario`, `Case`, `Regression`, etc.
1. Trait keys should be consistent across the repo:
   * `Type`, `Area`, `Scenario`, `Bug`
### Examples

```csharp
[Trait("Type","UnitTest")]
[Trait("Area","Schema")]
public class ColumnSchemaTests
{
    [Fact]
    public void Ctor_Rejects_Blank_Name() { /* ... */ }
}

[Trait("Type","IntegrationTest")]
[Trait("Area","Schema")]
public class ColumnSchemaIntegrationTests
{
    [Fact]
    public async Task Reads_Schema_From_Real_Database() { /* ... */ }
}
```

## 4) Determinism and isolation (non-negotiable)

1. No dependency on current time/timezone/random unless controlled:
   * Inject a clock/time provider.
   * Use fixed seeds for randomness.
1. No real user file paths:
   * Use temp directories and clean up.
3. Tests must be repeatable on any dev machine and in CI.

## 5) Parallelization and shared state

1. Unit tests must be **parallel-safe** by default.
2. Integration tests:
   * If shared external state exists (DB, filesystem location, container), declare an xUnit **Collection**.
   * Prefer one of:
     * `ICollectionFixture<T>` for shared expensive setup.
     * `IClassFixture<T>` for per-class expensive setup.

Example:

```csharp
[Collection("Database")]
public class SchemaIntegrationTests { /* ... */ }
```

If needed, disable parallelization at the **integration test project** level (not globally for all tests).

## 6) Reuse test setup and data

1. Use xUnit fixtures for shared setup:
   * `IClassFixture` for per-class setup.
   * `ICollectionFixture` for cross-class shared setup.
1. Parameterized tests:
   * Use `[Theory]` + `InlineData` for primitives.
   * Use `MemberData` / `ClassData` or Builders for complex objects.
3. Prefer readable builders and helpers over copy-paste Arrange blocks.

## 7) Unit vs Integration test boundaries

1. Prefer unit tests for pure logic:
   * configuration validation
   * mapping
   * safety rules
   * orchestration control flow
1. Use integration tests for code that talks to real infrastructure:
   * real database access and schema inspection
   * real filesystem behavior
   * real serialization formats (when behavior depends on runtime IO)

Do not create integration tests “just because.” Use them where correctness depends on the real dependency.

## 8) Coverage expectations

1. For each public method, cover:
   * happy path
   * key edge cases
   * error/exception paths
2. Focus on behavior, not implementation details.

## 9) Method naming

* Use: `MethodName_Scenario_ExpectedBehavior`
* Keep names explicit and searchable.

## 10) Static analysis and suppressions (keep it surgical)

1. Avoid broad file-level suppressions unless there is a strong reason.
2. Prefer local, explicit intent over disabling analyzers globally.
3. If you must suppress warnings, scope it narrowly:

   * suppress a line or a block, not the whole file.

## 11) Running tests (filters)

CI and local runs should use Trait filters:

* Unit:
  * `dotnet test --filter "Type=Unit"`
* Integration:
  * `dotnet test --filter "Type=Integration"`

If there is a single combined test project, the filter is mandatory to keep fast feedback.
## 12) Integration test readability

For integration tests, extract heavy Arrange steps into:
* fixtures
* helper methods
* builders
Keep the test body focused on the behavior being validated and the expected side effects.

## 13) Refactoring & Optimization Standards

To ensure test maintainability and architectural integrity, all test code must adhere to these optimization patterns:

### A) Test Infrastructure & Reusability

1. **Extract Shared Test Doubles**: Do not define `Fake` or `Mock` classes inside the test file. Move them to a centralized location (e.g., `ReportSyncer.Tests/Helpers/TestDoubles/`) to allow reuse across different test suites.
2. **Test Object Builders**: Avoid using long constructors for DTOs or Config objects inside the `Arrange` block. Implement a **Builder Pattern** or a **TestData Factory** (e.g., `JobBuilder`, `ConfigFactory`) to provide semantic defaults and reduce boilerplate.
3. **Shared Test Base**: If multiple test classes share identical setup logic (e.g., initializing the same three Mocks), extract them into a `TestBase` class (e.g., `SchemaServiceTestBase`) to keep individual tests focused on unique logic.
### B) Parameterization & Logic Compression

1. **Mandatory Theory Usage**: For scenarios testing multiple variations of the same logic (e.g., invalid identifiers, empty strings, null checks), you **must** use `[Theory]` with `[InlineData]` instead of creating multiple `[Fact]` methods.
2. **Deduplicate Arrange Logic**: Use factory methods with optional parameters to create complex objects, ensuring that a change in a constructor doesn't break 50 different tests.
### C) Behavioral Verification & Robustness

1. **Enhanced Call Tracking**: Fakes must be capable of behavioral verification. Include a `List<T> CapturedRequests` or `CallCount` property in `Fake` classes to track how many times and with what arguments a dependency was invoked.
2. **Error Simulation**: Fakes and Mocks must support exception injection (e.g., an `ExceptionToThrow` property) to test how the service handles transient database or network failures.
### D) Readability & Style

1. **Strict AAA Annotations**: Every test method must clearly label the `// Arrange`, `// Act`, and `// Assert` phases to guide the reader.
2. **No Magic Strings**: Replace hardcoded values with constants (e.g., `TestData.DefaultSourceTable`) at the class or project level to prevent "magic string" drift.
3. **Fluent Chain Assertions**: Maximize the use of **FluentAssertions** chainable syntax (e.g., `.Should().BeTrue().And.Contain("error")`) for more compact and readable validation.