---
applyTo: "ReportSyncer.Console/**/*.cs"
description: Rules for code inside ReportSyncer.Console (host layer)
---

# ReportSyncer.Console (Host) — Copilot Instructions

## Scope
You are working in **ReportSyncer.Console** (the Console Host).
This project is a **thin shell**: composition root + CLI + UI rendering + exit codes + logging.
Do NOT implement business rules here.

## Single source of truth
- Follow `docs/60_Console Host Spec.md` and `docs/61_Console Implementation Plan.md` as authoritative behavior contracts.
- Console must NOT re-implement Core logic (schema inspection, dependency planning, safety rules, SQL generation, DML decisions).

## Hard layering rules
- `ReportSyncer.Console` may depend on `ReportSyncer.Core` and `DotNetToolkit.*`.
- Never move domain logic from Core into Console.
- Prefer injecting Core services and wiring them in Program/Composition Root.

## Threading + UI rules
- Only the UI loop/thread may render to the console.
- Progress callbacks/reporters must **enqueue** events only (Channel/ConcurrentQueue). No rendering, no blocking I/O, no exceptions.

## Logging rules
- All logs go through `DotNetToolkit.Logging.ILogService`.
- Console Host may configure Serilog sinks, but Core must not reference Serilog directly.
- File logging should be JSONL. Keep log messages structured (message templates + properties).

## CLI + config rules
- CLI stays minimal: `--config <path>`, `--dry-run [true|false]`.
- `appsettings.json` provides defaults; CLI overrides appsettings.
- Console does not merge appsettings into YAML job config.

## Exit codes
- Map exceptions/results to exit codes exactly as specified in `docs/60_Console Host Spec.md`.

## Code style
- Use modern C# and nullable reference types.
- Keep methods small, testable, and avoid static state.
- Avoid hidden behavior: prefer explicit options + validation with clear error messages.
