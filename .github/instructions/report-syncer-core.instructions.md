---
description: Rules for code inside ReportSyncer.Core (domain layer)
applyTo: "src/ReportSyncer.Core/**"
---

- Treat ReportSyncer.Core as the domain layer:
  - No direct references to ReportSyncer.Console or ReportSyncer.WebApi.
  - No references to UI, HTTP, CLI, or environment-specific APIs.
  - No Serilog, no DryIoc, no Dapper, no JSON libraries. Use only DotNetToolkit.* and domain abstractions.

- Responsibilities that belong here:
  - Configuration models, YAML loading and validation.
  - Schema inspection, mapping, and FK dependency resolution.
  - Safety and permission checks.
  - Sync orchestration (`ISyncOrchestrator`, `PreFlightValidator`, `TableRunner`, `IDataWriter`).
  - Domain observability models (`RunSummary`, `TableResult`, `WorkEstimator`, `ProgressTracker`).

- When adding new behavior:
  - Prefer to add or extend services described in the architecture docs instead of inventing new entry points.
  - Ensure public methods throw the correct domain exception type for their responsibility.
  - Keep logic unit-testable; no static singletons, avoid hard-coded connection strings or paths.

- When generating SQL for sync behavior:
  - Use the domain-level SQL builder and DotNetToolkit.Database abstractions.
  - Do not use ORMs or ad-hoc raw ADO.NET access here.
