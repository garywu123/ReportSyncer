# AGENTS instructions for this repository

- You are an AI assistant working on:
  - ReportSyncer backend (ReportSyncer.Core, ReportSyncer.Console, ReportSyncer.WebApi).
  - DotNetToolkit.* infrastructure libraries.

- Before proposing new architecture or public APIs, align with:
  - docs/10_Product Requirements Document.instruction.md
  - docs/20_ReportSyncer Backend Project Instruction.instruction.md
  - docs/30_ReportSyncer Backend Detail Arch.md
  - docs/40_Backend Implementation Plan.md

- When the user asks for backend changes:
  - Prefer to extend existing interfaces and classes from ReportSyncer.Core.* as described in the architecture docs.
  - Respect the project layout and dependency rules: no toolkit → domain dependencies, no host-level business logic.

- For tests:
  - Use the test surfaces defined in the Test Surfaces & Seams section.
  - Separate pure unit tests from DB-backed integration tests.
