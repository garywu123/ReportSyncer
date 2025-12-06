
## 1. What is “Effective configuration”?

You already have:

* **Base configuration**: `SyncConfiguration` loaded from YAML by `YamlConfigurationLoader` and validated by `ConfigurationValidator`.【turn9file0†40_Backend Implementation Plan.md†L23-L43】【turn8file15†40_Backend Implementation Plan.md†L41-L62】
* **Overrides**: host-supplied knobs like “dry-run” and “batch sizes” for this *run only*, not saved back into YAML.【turn9file0†40_Backend Implementation Plan.md†L25-L31】【turn9file10†10_Product Requirements Document (PRD).md†L79-L83】

**Effective configuration** = a `SyncConfiguration` instance that:

1. Starts from the validated YAML config.
2. Applies a *narrow*, explicit set of overrides (e.g. dryRun, default batch size).
3. Is immutable for the rest of the pipeline (schema, safety, sync only see this one view).【turn9file0†40_Backend Implementation Plan.md†L39-L43】【turn9file2†40_Backend Implementation Plan.md†L9-L12】

So: *Base* = YAML, *Overrides* = host knobs, *Effective* = merged result the engine actually executes.

---

## 2. Assumptions for Task 2.4

To avoid hand-wavy nonsense, let’s pin down what is actually overridable in v1:

From PRD + existing domain model:

* `RunConfig` holds “global settings (Batch sizes, Dry-run defaults)”【turn9file10†10_Product Requirements Document (PRD).md†L79-L83】:

  * `DryRun : bool`
  * `DefaultBatchSize : int`
  * `DeleteChunkSize : int`
  * `UseTvpIfAvailable : bool`
  * `EtaSmoothing : double` (already validated in `ConfigurationValidator`).【turn8file14†configuration.xml†L7-L13】

All the scary stuff (safety thresholds, prod/prod rules, allowAllDelete etc.) stays *non-overridable*.

So overrides for Task 2.4 will be:

* **Global run-level only**:

  * `DryRun`
  * `DefaultBatchSize`
  * `DeleteChunkSize`
  * `UseTvpIfAvailable`
  * Optionally `EtaSmoothing` if you want to expose it; I’ll include it in the design.

No per-table overrides in v1 to keep the surface area sane.

---

## 3. Detailed implementation steps for Task 2.4

### Step 0 – Add high-level comment block in the implementation plan

At the top of the Task 2.4 section in your “implementation plan for coders” (not code), paste a short explanation:

* Base vs overrides vs effective config.
* List of overridable fields (as above).
* Rule: **merge happens only inside `ReportSyncer.Core.Configuration` before anything touches DB**.【turn9file0†40_Backend Implementation Plan.md†L56-L63】

That gives your AI coder context without forcing it to re-open PRD/arch.

---

### Step 1 – Introduce `RuntimeOverrides` model

**File:** `ReportSyncer.Core/Configuration/RuntimeOverrides.cs`

1. Create a class:

   ```csharp
   namespace ReportSyncer.Core.Configuration;

   public sealed class RuntimeOverrides
   {
       public bool? DryRun { get; init; }
       public int? DefaultBatchSize { get; init; }
       public int? DeleteChunkSize { get; init; }
       public bool? UseTvpIfAvailable { get; init; }
       public double? EtaSmoothing { get; init; }
   }
   ```

   Purpose: **host-supplied knobs for a single run**, not persisted, not YAML, purely runtime.

2. Add XML docs explaining:

   * This is *not* configuration; it’s an optional runtime overlay.
   * Every property is nullable: `null` means “no override, use YAML”.
   * Only run-level fields are allowed here by design (safety & schema policy are intentionally non-overridable).

3. Add a small helper method:

   ```csharp
   public bool IsEmpty()
   {
       return DryRun is null
           && DefaultBatchSize is null
           && DeleteChunkSize is null
           && UseTvpIfAvailable is null
           && EtaSmoothing is null;
   }
   ```

   So consumers can early-exit when nothing is set.

---

### Step 2 – Add an internal merge helper (`ConfigurationMerger`)

**File:** `ReportSyncer.Core/Configuration/ConfigurationMerger.cs`

Purpose: centralize all override merge rules so they’re not leaked into orchestrators or other modules.【turn9file0†40_Backend Implementation Plan.md†L51-L58】

1. Create internal static class:

   ```csharp
   internal static class ConfigurationMerger
   {
       public static SyncConfiguration ApplyOverrides(
           SyncConfiguration baseConfig,
           RuntimeOverrides? overrides)
       { ... }
   }
   ```

2. Inside `ApplyOverrides`:

   1. **Guard clauses**

      * Throw `ArgumentNullException` if `baseConfig` is null.
      * If `overrides` is `null` or `overrides.IsEmpty()`:

        * Return `baseConfig` *as-is* (no clone).

   2. **Validate override values**

      Use same rules as `ConfigurationValidator` for run numeric ranges (batch size > 0, delete chunk size > 0, `EtaSmoothing` in `(0,1]` etc.). You already encode these in `ConfigurationValidator` via `ConfigurationErrors.InvalidBatchSize(...)`, `InvalidDeleteChunkSize(...)`, `InvalidEtaSmoothing(...)`.【turn8file14†configuration.xml†L7-L13】

      Here, if an override is invalid:

      * Throw `ConfigurationException` with a single `ConfigurationError` representing the bad override.
        This lines up with Task 2.5 (“other config-related code (overrides, effective config) uses the same type”).【turn9file2†40_Backend Implementation Plan.md†L50-L56】

   3. **Build new `RunConfig`**

      * Read `var run = baseConfig.Run;`.
      * Create a *new* `RunConfig` instance, applying override if present:

        * `dryRun` = `overrides.DryRun ?? run.DryRun`
        * `defaultBatchSize` = `overrides.DefaultBatchSize ?? run.DefaultBatchSize`
        * `deleteChunkSize` = `overrides.DeleteChunkSize ?? run.DeleteChunkSize`
        * `useTvpIfAvailable` = `overrides.UseTvpIfAvailable ?? run.UseTvpIfAvailable`
        * `etaSmoothing` = `overrides.EtaSmoothing ?? run.EtaSmoothing`

      Do **not** mutate the existing `RunConfig` instance; always construct a new one so the original `SyncConfiguration` is untouched.

   4. **Clone `SyncConfiguration` with new `RunConfig`**

      * Keep:

        * `Version`
        * `Safety`
        * `SchemaPolicy`
        * `Connections`
        * `SyncJobs`
          exactly as they are in `baseConfig`. Only `Run` is replaced.

      So roughly:

      ```csharp
      var effective = new SyncConfiguration(
          baseConfig.Version,
          newRunConfig,
          baseConfig.Safety,
          baseConfig.SchemaPolicy,
          baseConfig.Connections,
          baseConfig.SyncJobs);
      ```

      That gives you the **effective configuration** instance.

3. Optionally (but recommended): add XML doc on `ApplyOverrides` describing:

   * “Returns `baseConfig` unchanged if overrides are null/empty.”
   * “Throws `ConfigurationException` if overrides would lead to invalid run settings.”

You’re centralizing the merge and keeping the surface small and testable.

---

### Step 3 – Extend `IConfigurationProvider` to accept overrides

Current architecture says:【turn8file15†30_ReportSyncer Backend Arch.md†L4-L8】

```csharp
public interface IConfigurationProvider
{
    Task<SyncConfiguration> LoadAndValidateAsync(string path, CancellationToken ct);
}
```

You don’t break this; you **add** an overload:

1. Update `ReportSyncer.Core/Configuration/IConfigurationProvider.cs`:

   ```csharp
   public interface IConfigurationProvider
   {
       Task<SyncConfiguration> LoadAndValidateAsync(
           string path,
           CancellationToken ct);

       Task<SyncConfiguration> LoadAndValidateAsync(
           string path,
           RuntimeOverrides? overrides,
           CancellationToken ct);
   }
   ```

2. Document the behavior of the new overload:

   * `path` is YAML file path.
   * `overrides` is optional; if null or empty, results are identical to the original overload.
   * Throws `ConfigurationException` if:

     * YAML is bad (loader).
     * Static rules fail (validator).
     * Override values are invalid (merger).

This gives hosts a clean, dedicated entry point to get the **effective** config. Orchestrator later should only ever use `IConfigurationProvider` and never manually play with `RuntimeOverrides`.

---

### Step 4 – Implement override-aware `ConfigurationProvider`

**File:** `ReportSyncer.Core/Configuration/ConfigurationProvider.cs` (already exists).【turn8file15†30_ReportSyncer Backend Arch.md†L4-L8】

Add constructor & fields if not already present:

* `_loader : IConfigurationLoader`
* `_validator : IConfigurationValidator`
* `_log : ILogService`

Then:

1. Keep existing method as thin wrapper:

   ```csharp
   public Task<SyncConfiguration> LoadAndValidateAsync(
       string path,
       CancellationToken ct)
   {
       return LoadAndValidateAsync(path, overrides: null, ct);
   }
   ```

2. Implement the new overload:

   ```csharp
   public async Task<SyncConfiguration> LoadAndValidateAsync(
       string path,
       RuntimeOverrides? overrides,
       CancellationToken ct)
   {
       ArgumentException.ThrowIfNullOrWhiteSpace(path);

       // 1. Load YAML into base SyncConfiguration
       var baseConfig = await _loader.LoadAsync(path, ct).ConfigureAwait(false);

       // 2. Validate static rules on the base config
       _validator.Validate(baseConfig);

       // 3. Apply overrides (if any) and validate override values
       var effective = ConfigurationMerger.ApplyOverrides(baseConfig, overrides);

       // No extra validation invocation; the merger itself guarantees override safety.
       return effective;
   }
   ```

Notes:

* Step 2 uses existing `IConfigurationValidator` which already checks batch size, delete chunk size, eta smoothing etc. on the YAML values.【turn8file14†configuration.xml†L7-L13】
* Step 3 ensures override values are in the same valid range before constructing `RunConfig`. If override is invalid, you throw `ConfigurationException` there.

If you wanted to be even stricter, you could re-run `_validator.Validate(effective)` after merge, but that’s redundant if overrides are limited to run-level numeric flags and you keep their validation in `ConfigurationMerger`.

---

### Step 5 – Keep overrides localized

To enforce “no scattered manual overrides” from the plan,【turn9file2†40_Backend Implementation Plan.md†L6-L7】 you need to be explicit:

1. In XML docs on `ConfigurationProvider` and/or a comment at top of `ConfigurationMerger`, state:

   * “All runtime overrides must flow through `RuntimeOverrides` + `ConfigurationMerger` + `IConfigurationProvider.LoadAndValidateAsync(path, overrides, ct)`.”

2. Any later components (`SyncOrchestrator`, `PreFlightValidator`, etc.) should only accept a **single** `SyncConfiguration` and must not take `RuntimeOverrides` themselves.

This is design pressure so future-you doesn’t scatter “if (cliDryRun) …” everywhere.

---

### Step 6 – Optional: helper factory for common host scenarios

You don’t *need* this for Task 2.4, but it makes life easier for hosts:

**File:** `ReportSyncer.Core/Configuration/RuntimeOverridesFactory.cs` (or static methods somewhere)

Add simple helpers the CLI/WebAPI can use:

```csharp
public static class RuntimeOverridesFactory
{
    public static RuntimeOverrides FromCliOptions(
        bool? dryRunFlag,
        int? defaultBatchSize,
        int? deleteChunkSize)
    {
        return new RuntimeOverrides
        {
            DryRun = dryRunFlag,
            DefaultBatchSize = defaultBatchSize,
            DeleteChunkSize = deleteChunkSize
            // Leave UseTvpIfAvailable / EtaSmoothing null for now if CLI doesn’t expose them.
        };
    }
}
```

This keeps host-side code dumb: gather flags, call factory, pass to config provider.

---

### Step 7 – Outline of tests for Task 2.4 (for later)

You said this prompt is for implementation steps, but you *will* need tests, so keep this list close:

1. **Unit tests for `ConfigurationMerger`**:

   * `ApplyOverrides_WithNullOverrides_ReturnsBaseConfigInstance`.
   * `ApplyOverrides_WithEmptyOverrides_ReturnsBaseConfigInstance`.
   * `ApplyOverrides_WithDryRunOverride_ReplacesRunDryRunOnly`.
   * `ApplyOverrides_WithBatchSizeOverride_ReplacesRunDefaultBatchSizeOnly`.
   * `ApplyOverrides_WithInvalidBatchSize_ThrowsConfigurationException`.
   * `ApplyOverrides_WithMultipleOverrides_ProducesExpectedRunConfig`.

2. **Unit tests for `ConfigurationProvider`**:

   * Use mocks/fakes for `IConfigurationLoader`, `IConfigurationValidator`:

     * `LoadAndValidateAsync_WithoutOverrides_CallsLoaderAndValidatorOnce_ReturnsBaseConfig`.
     * `LoadAndValidateAsync_WithOverrides_CallsLoaderAndValidatorOnce_AppliesOverrides`.
     * `LoadAndValidateAsync_WithInvalidOverrides_ThrowsConfigurationException_FromMerger`.

   This matches how the architecture treats `IConfigurationProvider` as a thin orchestrator.【turn9file3†20_ReportSyncer Backend Architecture.md†L73-L80】

---

## 4. Summary in your terms

* **Effective configuration** = `SyncConfiguration` **after** merging optional `RuntimeOverrides`, used by everything downstream.
* You add:

  * `RuntimeOverrides` (host knobs).
  * `ConfigurationMerger.ApplyOverrides(...)` (merge logic + override validation).
  * An overload on `IConfigurationProvider.LoadAndValidateAsync` that accepts overrides and returns the effective config.
* You *don’t*:

  * Touch safety config, schema policy, or table-level options for overrides v1.
  * Let anything outside `ReportSyncer.Core.Configuration` fiddle with overrides directly.

This gives you a precise, small surface for hosts to influence runtime behavior without polluting the rest of the system, and without smearing “dry-run flag” logic all over the codebase. Which, frankly, is a minor miracle in most enterprise apps.
