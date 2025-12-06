
# Task 2.3 Configuration Validator Unit Tests

Below is a **step-by-step plan** for all unit tests around `ConfigurationValidator`, including YAML test material & helpers, ordered so an AI coder (or a human on autopilot) can just go top-to-bottom. This is based on the current validator & error factories in `ReportSyncer.Core.Configuration`. 

---

## Phase 0 – Test layout & basic wiring

**Step 0.1 – Create test folder & namespace**

* Folder: `Tests/ReportSyncer.Core.Tests/Configuration`
* Main file: `ConfigurationValidatorTests.cs`
* Namespace: `ReportSyncer.Core.Tests.Configuration`
* Use the same test framework the repo already uses (don’t invent a new one).

**Step 0.2 – Create a small test helper class**

Create `ConfigurationTestData.cs` in the same test namespace with:

* A static method:

  ```csharp
  public static SyncConfiguration CreateMinimalValidConfig()
  ```

  that returns a *fully valid* config according to the current validator:

  * Non-null `Connections` with 1 valid connection:

    * `Name` non-empty
    * `Environment` a valid `EnvironmentType`
    * `Type` a valid `ConnectionType`
    * `ConnectionString` non-empty
  * Non-null `Jobs` with 1 valid job:

    * `Name` non-empty
    * `SourceConnection` & `TargetConnection` reference the existing connection
    * `Tables` contains 1 valid table:

      * `Source` non-empty
      * `Target` non-empty
      * Delete mode & thresholds set so **no** safety errors are triggered
  * `Run` has:

    * `DefaultBatchSize > 0`
    * `DeleteChunkSize > 0`
    * `EtaSmoothing` in `[0, 1]` (pick mid value like `0.5`)
  * `Safety` has:

    * `ConfirmLargeDeletePct` in `[0, 1]` (e.g. `0.1`)
  * `Version` set to some non-empty string that should be accepted (`"1.0"`).

This is the “golden” config you mutate for every negative test.

---

## Phase 1 – YAML test material & loader helper

You wanted YAML involved, so here’s a minimal but structured plan.

**Step 1.1 – Create YAML test data folder**

Under test project:

* `Tests/ReportSyncer.Core.Tests/TestData/Configuration`

Create the following YAML files (You can re-use any existing YAML files in TestFiles folder.):

1. `valid-minimal.yml` 

   * Mirrors `CreateMinimalValidConfig` but in YAML form.
2. `valid-full.yml`

   * Richer config with multiple connections, jobs, tables, filters, etc.
3. `invalid-connections.yml`

   * Broken connection scenarios: duplicates, invalid env, missing connection string.
4. `invalid-jobs.yml`

   * Broken job scenarios: unknown connection references, no tables, duplicate names.
5. `invalid-tables.yml`

   * Broken table scenarios: missing source/target, invalid delete thresholds, unsafe full delete.
6. `invalid-safety.yml`

   * Broken `Run` / `Safety` values: invalid batch size, invalid delete chunk, invalid percentages.

**Step 1.2 – Implement YAML loading helper**

In a new file `ConfigurationYamlTestHelper.cs`:

* Add something like:

  ```csharp
  public static class ConfigurationYamlTestHelper
  {
      public static SyncConfiguration LoadFromYamlFile(string relativePath)
      {
          // Combine base test directory + relativePath
          // Use the same YAML loader the production code uses
          // e.g. YamlConfigurationLoader.LoadAsync then .Result, or a sync wrapper
      }
  }
  ```

* This is **integration-ish** but still lives in the test project.

* Use this helper only for a few full-flow tests (see Phase 6) so you don’t blow up the number of YAMLs.

---

## Phase 2 – Happy-path tests (object-based, no YAML)

Goal: prove validator doesn’t complain on sane configs *before* you start breaking everything.

**Step 2.1 – Minimal valid configuration**

Test: `Validate_MinimalValidConfiguration_DoesNotThrow`

* Arrange: `var config = ConfigurationTestData.CreateMinimalValidConfig();`
* Act/Assert: `ConfigurationValidator.Validate(config)` does **not** throw.

**Step 2.2 – Full rich configuration**

Test: `Validate_FullValidConfiguration_DoesNotThrow`

* Arrange:

  * Start from minimal config.
  * Add a second connection (different name, valid env / type / string).
  * Add a second job using both connections.
  * Add multiple tables with:

    * Different sources/targets
    * Optional filters, column mappings, keys, sync options filled in with “sane” values.
* Act/Assert: `Validate` does not throw.

If this fails, the validator rules and the configuration model are not aligned; fix that first.

---

## Phase 3 – Root-level validation tests

Validator behavior from `ValidateRoot` and null/empty checks.

**Step 3.1 – Null root configuration**

Test: `Validate_WithNullConfiguration_ThrowsArgumentNullException`

* Act: `validator.Validate(null)` → expect `ArgumentNullException`.

**Step 3.2 – Null Connections collection**

Test: `Validate_WithNullConnectionsCollection_ReportsNullConnectionsError`

* Arrange: `config.Connections = null;`
* Act: catch `ConfigurationException`.
* Assert:

  * Contains `ConfigurationErrors.NullConnectionsCollection().Code` in `Errors`.

**Step 3.3 – Empty Connections collection**

Test: `Validate_WithEmptyConnectionsCollection_ReportsNoConnectionsDefined`

* Arrange: `config.Connections = new List<ConnectionConfig>();`
* Act: catch `ConfigurationException`.
* Assert:

  * Contains `CFG_CONNECTIONS_EMPTY` (`NoConnectionsDefined`).

**Step 3.4 – Null Jobs collection**

Test: `Validate_WithNullJobsCollection_ReportsNullJobsError`

* Arrange: `config.Jobs = null;`
* Assert for `NullJobsCollection` code.

**Step 3.5 – Empty Jobs collection**

Test: `Validate_WithEmptyJobsCollection_ReportsNoJobsDefined`

* Arrange: `config.Jobs = new List<JobConfig>();`
* Assert for `NoJobsDefined`.

**Step 3.6 – Missing Run settings**

Test: `Validate_WithNullRunSettings_ReportsMissingRunSettings`

* Arrange: `config.Run = null;`
* Assert error code `MissingRunSettings`.

**Step 3.7 – Missing Safety settings**

Test: `Validate_WithNullSafetySettings_ReportsMissingSafetySettings`

* Arrange: `config.Safety = null;`
* Assert error code `MissingSafetySettings`.

---

## Phase 4 – Connections validation tests

Based on `ValidateConnections` logic.

**Step 4.1 – Missing connection name**

Test: `Validate_WithConnectionMissingName_ReportsConnectionNameMissing`

* Arrange:

  * One connection with `Name = null` or whitespace.
* Assert:

  * Single error with code `ConnectionNameMissing`.
  * Path like `"connections[0]"`.

**Step 4.2 – Duplicate connection names**

Test: `Validate_WithDuplicateConnectionNames_ReportsDuplicateConnectionName`

* Arrange:

  * Two connections with same trimmed `Name`.
* Assert:

  * One `DuplicateConnectionName` error.
  * Error message references both indices (you don’t need to assert exact text, just code + path).

**Step 4.3 – Invalid environment type**

Test: `Validate_WithInvalidEnvironmentType_ReportsInvalidConnectionEnvironment`

* Arrange:

  * Set `Environment` to a value not defined in `EnvironmentType` enum
    *or* to a sentinel like `Unknown` if you use one.
* Assert:

  * `InvalidConnectionEnvironment` error.

**Step 4.4 – Invalid connection type**

Test: `Validate_WithInvalidConnectionType_ReportsInvalidConnectionType`

* Arrange:

  * `Type` set outside enum or to disallowed sentinel.
* Assert:

  * `InvalidConnectionType` error.

**Step 4.5 – Missing connection string**

Test: `Validate_WithMissingConnectionString_ReportsConnectionStringMissing`

* Arrange:

  * `ConnectionString` null or whitespace.
* Assert:

  * `ConnectionStringMissing` error.

---

## Phase 5 – Jobs validation tests

Based on `ValidateJobs`.

**Step 5.1 – Missing job name**

Test: `Validate_WithJobMissingName_ReportsJobNameMissing`

* Arrange:

  * Single job with `Name = null` or whitespace.
* Assert:

  * `JobNameMissing` with path `"jobs[0]"`.

**Step 5.2 – Duplicate job names**

Test: `Validate_WithDuplicateJobNames_ReportsDuplicateJobName`

* Arrange:

  * Two jobs with same `Name`.
* Assert:

  * `DuplicateJobName` error with path for the second.

**Step 5.3 – Missing source connection**

Test: `Validate_WithJobMissingSourceConnection_ReportsJobSourceConnectionMissing`

* Arrange:

  * `SourceConnection = null` or whitespace.
* Assert:

  * `JobSourceConnectionMissing`.

**Step 5.4 – Unknown source connection**

Test: `Validate_WithJobUnknownSourceConnection_ReportsJobSourceConnectionUnknown`

* Arrange:

  * `SourceConnection = "DoesNotExist"`; only existing connection called `"Existing"`.
* Assert:

  * `JobSourceConnectionUnknown`.

**Step 5.5 – Missing target connection**

Test: `Validate_WithJobMissingTargetConnection_ReportsJobTargetConnectionMissing`

* Similar to 5.3 but for target.

**Step 5.6 – Unknown target connection**

Test: `Validate_WithJobUnknownTargetConnection_ReportsJobTargetConnectionUnknown`

* Similar to 5.4 but for target.

**Step 5.7 – Job has no tables**

Test: `Validate_WithJobHavingNoTables_ReportsJobHasNoTables`

* Arrange:

  * `job.Tables = new List<TableConfig>()`.
* Assert:

  * `JobHasNoTables`.

**Step 5.8 – Prod to Prod sync forbidden**

Test: `Validate_WithProdToProdJob_ReportsProdToProdSyncNotAllowed`

* Arrange:

  * Two connections: `Source` and `Target` both with `EnvironmentType.Prod`.
  * Job referencing those connections.
  * Safety config such that prod-to-prod is *not* explicitly allowed.
* Assert:

  * `ProdToProdSyncNotAllowed` error with correct job path and connection names in message.

---

## Phase 6 – Tables validation tests

Based on `ValidateTables` & `ValidateTableDeleteSettings`.

**Step 6.1 – Missing source table name**

Test: `Validate_WithTableMissingSource_ReportsSourceTableMissing`

* Arrange:

  * Table with `Source = null` or whitespace.
* Assert:

  * `SourceTableMissing`.

**Step 6.2 – Missing target table name**

Test: `Validate_WithTableMissingTarget_ReportsTargetTableMissing`

* Arrange:

  * `Target = null` or whitespace.
* Assert:

  * `TargetTableMissing`.

**Step 6.3 – Invalid delete threshold**

Test: `Validate_WithDeleteThresholdLessOrEqualZero_ReportsDeleteThresholdOutOfRange`

* Arrange:

  * `DeleteThreshold = 0` or negative.
* Assert:

  * `DeleteThresholdOutOfRange` with reported value.

Test: `Validate_WithDeleteThresholdGreaterThanOne_ReportsDeleteThresholdOutOfRange`

* Arrange:

  * `DeleteThreshold = 1.1` or other > 1.
* Assert same code.

**Step 6.4 – Full table delete without safety latch**

Test: `Validate_WithFullTableDeleteWithoutSafety_ReportsFullTableDeleteNotAllowed`

* Arrange:

  * Table `PreSyncDeleteMode = FullTable` (or equivalent).
  * No table/job/global flag that allows it.
* Assert:

  * `FullTableDeleteNotAllowed`.

**Step 6.5 – Placeholder validations (filters, sync options, column mappings, keys)**

The current code has stub validators (`ValidateFilter`, `ValidateSyncOptions`, `ValidateColumnMapping`, `ValidateKeys`) that basically “accept anything for now”.

* For now, add **one** test that confirms they don’t crash:

  Test: `Validate_WithBasicFilterSyncOptionsKeys_DoesNotThrow`

  * Arrange:

    * Minimal valid config.
    * For table:

      * `Filter` non-null with some dummy fields.
      * `SyncOptions` non-null with sane values.
      * `ColumnMapping` non-null with at least one mapping.
      * `Keys` non-null with at least one key column.
  * Assert:

    * Validation still passes.

Once you add real rules to those helpers, you add corresponding negative tests.

---

## Phase 7 – Run & Safety configuration tests

Based on `ValidateSafety`.

**Step 7.1 – Invalid batch size**

Test: `Validate_WithNonPositiveDefaultBatchSize_ReportsInvalidBatchSize`

* Arrange:

  * `Run.DefaultBatchSize = 0` (or negative).
* Assert:

  * `InvalidBatchSize`.

**Step 7.2 – Invalid delete chunk size**

Test: `Validate_WithNonPositiveDeleteChunkSize_ReportsInvalidDeleteChunkSize`

* Arrange:

  * `Run.DeleteChunkSize = 0` (or negative).
* Assert:

  * `InvalidDeleteChunkSize`.

**Step 7.3 – Invalid ETA smoothing**

Test: `Validate_WithEtaSmoothingLessThanZero_ReportsInvalidEtaSmoothing`

* Arrange: `Run.EtaSmoothing = -0.1`.

Test: `Validate_WithEtaSmoothingGreaterThanOne_ReportsInvalidEtaSmoothing`

* Arrange: `Run.EtaSmoothing = 1.1`.

**Step 7.4 – Invalid confirm large delete percentage**

Test: `Validate_WithConfirmLargeDeletePctOutOfRange_ReportsConfirmLargeDeletePctOutOfRange`

* Arrange:

  * `Safety.ConfirmLargeDeletePct = -0.01` or `1.5`.
* Assert:

  * `ConfirmLargeDeletePctOutOfRange`.

**Step 7.5 – Version format acceptance**

Right now `ValidateConfigurationVersion` basically accepts any non-empty string.

* Test: `Validate_WithNonEmptyVersion_DoesNotThrow`

  * `Version = "1.0.0"`.
  * Assert no error.
* You **don’t** need negative tests here until you add real version rules.

---

## Phase 8 – Error aggregation behavior

**Step 8.1 – Multiple violations collected in one exception**

Test: `Validate_WithMultipleViolations_CollectsAllErrors`

* Arrange:

  * Start from minimal valid config.
  * Introduce at least 3 independent violations, e.g.:

    * `Connections` has duplicate names.
    * A job has no tables.
    * `Run.DefaultBatchSize = 0`.
* Act: catch `ConfigurationException`.
* Assert:

  * `Errors.Count >= 3`.
  * Contains codes:

    * `CFG_CONNECTION_NAME_DUPLICATE`
    * `CFG_JOB_HAS_NO_TABLES`
    * `CFG_RUN_BATCH_SIZE_INVALID` (whatever the actual code strings are).

The point is to prove the “collect all errors, then throw” pattern works.

---

## Phase 9 – YAML-driven integration tests (minimal set)

Now use the YAML helpers so you have at least a few end-to-end checks without exploding the number of test cases.

**Step 9.1 – Valid minimal YAML passes**

Test: `Validate_ValidMinimalYaml_DoesNotThrow`

* Arrange:

  * `var config = ConfigurationYamlTestHelper.LoadFromYamlFile("valid-minimal.yml");`
* Assert:

  * `ConfigurationValidator.Validate(config)` does not throw.

**Step 9.2 – Valid full YAML passes**

Test: `Validate_ValidFullYaml_DoesNotThrow`

* Same as above with `valid-full.yml`.

**Step 9.3 – Invalid YAML samples map to correct error codes**

You don’t need 20 tests here; just 1 per YAML file:

* `Validate_InvalidConnectionsYaml_ReportsConnectionErrors`

  * Uses `invalid-connections.yml`.
  * Assert at least one of:

    * `NoConnectionsDefined` / `ConnectionNameMissing` / `DuplicateConnectionName` etc.

* `Validate_InvalidJobsYaml_ReportsJobErrors`

* `Validate_InvalidTablesYaml_ReportsTableErrors`

* `Validate_InvalidSafetyYaml_ReportsSafetyErrors`

End-to-end: YAML → loader → `SyncConfiguration` → `ConfigurationValidator` → `ConfigurationException`.

---

## Phase 10 – Rough implementation order

If you want the least context churn for an AI coder:

1. Implement `ConfigurationTestData.CreateMinimalValidConfig`.
2. Implement **Phase 2** happy-path tests first (2.1, 2.2).
3. Implement **Phase 3–7** negative tests in order.
4. Implement **Phase 8** aggregation tests.
5. Implement YAML helper + YAML test files (Phase 1).
6. Implement **Phase 9** YAML tests.

Do it in that sequence and you’ll keep context small and focused, instead of throwing the model a random mix of YAML, helper code, and half-finished tests like a puzzle.
