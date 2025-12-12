
# Copilot instructions for ReportSyncer & DotNetToolkit

## Overview
This document describes guidance for working across the ReportSyncer solution and the shared DotNetToolkit libraries. It covers repository contents, C# generation rules, layering constraints, exception usage, guidance for adding new behavior, and code style requirements.

## Repository Contents
### ReportSyncer
- ReportSyncer backend (domain + hosts) for database sync.

### DotNetToolkit
DotNetToolkit contains shared infrastructure libraries used by ReportSyncer and other products:
- `DotNetToolkit.General`: general-purpose utilities.
- `DotNetToolkit.Database`: database abstraction layer used across products.
- `DotNetToolkit.Logging`: logging abstractions.

### Documentation
Canonical backend docs live in `docs/`:
- `10_Product Requirements Document.instruction.md`
- `20_ReportSyncer Backend Project Instruction.instruction.md`
- `30_ReportSyncer Backend Detail Arch.md`
- `40_Backend Implementation Plan.md`

## C# Generation Guidelines
- **Target:** `net8.0` and modern C# features (records, pattern matching).
- **DI:** Use `Microsoft.Extensions.DependencyInjection` for DI abstractions.
- **Logging:** From domain code, log via `DotNetToolkit.Logging.ILogService` only. Hosts may wire Serilog; do not use Serilog directly in `ReportSyncer.Core`.
- **Database:** Use `DotNetToolkit.Database` abstractions (`IDbContext`, `IDbCommandWrapper`, etc.) for DB access. Do not use Entity Framework.

## Layering Rules
- `ReportSyncer.Core` may depend on `DotNetToolkit.*` but must never depend on `ReportSyncer.Console` or `ReportSyncer.WebApi`.
- `DotNetToolkit.*` must never depend on any `ReportSyncer.*` project or domain exception types.
- Hosts (`ReportSyncer.Console`, `ReportSyncer.WebApi`) are thin shells for DI, configuration, process/HTTP lifecycle, and progress output — do not add business rules in hosts.

## Exception Types (Preferred Domain Exceptions)
- `ConfigurationException` — YAML/argument/config problems.
- `SchemaMismatchException` — schema / dependency / mapping issues.
- `SafetyViolationException` — guardrail violations (e.g., prod→prod, unsafe deletes).
- `SyncExecutionException` — runtime DB/IO failures after pre-flight.
- `UserCancelledException` — when the user cancels a running job.

## Adding New Behavior in `ReportSyncer.Core`
- **Prefer** extending existing interfaces from the backend architecture docs (e.g., `IConfigurationLoader`, `ISchemaInspector`, `IDependencyResolver`, `ISyncOrchestrator`, `IDataWriter`, `IHistoryService`).
- Keep classes testable and side-effect free where possible; use abstractions like `IClock` and `INamedDbContextFactory`.
- Always add or update tests in `ReportSyncer.Tests` following the Test Surfaces & Seams guidance in the architecture docs.

## Code Style

### Documentation
1. Each class should include a summary of its responsibilities. Each method should have a summary, parameter descriptions, return information and exception.
### File Header Requirements
Each class file should include a header comment describing the file's purpose and any important details:
- **Author:** Gary Wu
- **Project:** ReportSyncer
- **Date:** Today's date
### XML Documentation
- Place different documentation purposes inside the appropriate XML tags.
- Wrap code examples inside a `CDATA` section within `<code>` if included in XML docs.
- Use `<remarks>` for additional important information about classes or methods.

### Terminology
- Use consistent terminology across the codebase.
