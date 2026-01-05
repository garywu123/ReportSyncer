# copilot #ReportSyncer

## Overview
This repository contains the ReportSyncer solution and shared DotNetToolkit libraries.
Follow strict layering. Keep hosts thin. Keep domain logic in Core.

## Solution Structure
- `ReportSyncer.Core`: domain + orchestration + schema/preflight/safety/sql/write logic.
- `ReportSyncer.Console`: Console Host (composition root + CLI + UI rendering + exit codes + logging).
- `ReportSyncer.WebApi`: Web API Host (thin shell).
- `DotNetToolkit.*`: shared infrastructure libraries (no dependency on ReportSyncer).

## Canonical Docs (Docs folder)
- `docs/10_Product Requirements Document.Instruction.md`
- `docs/20_ReportSyncer Backend Architecture.instruction.md`
- `docs/30_ReportSyncer Backend Detail Arch.md`
- `docs/40_Backend Implementation Plan.md`
- `docs/60_Console Host Spec.md`
- `docs/61_Console Implementation Plan.md`

## Instruction Files (Path-specific)
- `.github/instructions/report-syncer-core.instructions.md`: rules for `ReportSyncer.Core`
- `.github/instructions/report-syncer-console.instructions.md`: rules for `ReportSyncer.Console`
- `.github/instructions/tests.instructions.md`: shared testing rules (Core tests)
- `.github/instructions/report-syncer-console-tests.instructions.md`: rules for `ReportSyncer.Console.Tests`
- `.github/instructions/dotnetoolkit.instructions.md`: rules for `DotNetToolkit.*`

# C# Generation Guidelines
- Target `net10.0`, modern C# features, nullable reference types enabled.
- DI: use `Microsoft.Extensions.DependencyInjection`.
- Database: use `DotNetToolkit.Database` abstractions; do not use EF.
- Logging:
  - In Core: use `Microsoft.Extensions.Logging.ILogger<T>` only. Do not reference Serilog or `DotNetToolkit.Logging` from Core.
  - Hosts: configure Serilog as the `Microsoft.Extensions.Logging` provider (e.g. `AddSerilog`) and create the Serilog pipeline in the host bootstrap.

## Layering Rules (Hard)
- `ReportSyncer.Core` may depend on `DotNetToolkit.*` but must never depend on hosts.
- `DotNetToolkit.*` must never depend on any `ReportSyncer.*` project or domain exception types.
- Hosts (`ReportSyncer.Console`, `ReportSyncer.WebApi`) are thin shells. No business rules in hosts.

## Domain Exception Types (Preferred)
- `ConfigurationException` — YAML/argument/config problems.
- `SchemaMismatchException` — schema/dependency/mapping issues.
- `SafetyViolationException` — guardrail violations.
- `SyncExecutionException` — runtime DB/IO failures after pre-flight.
- `UserCancelledException` — user-initiated cancellation.

## Adding New Behavior
- Prefer extending existing interfaces in the architecture docs.
- Keep classes testable; use abstractions like `IClock` when needed.
- Always add/update tests in the appropriate test project.

## Code Style
- XML docs required for public types and key methods.
- Prefer clear, explicit naming and small methods.
- Avoid magic defaults: validate inputs early and provide actionable error messages.
