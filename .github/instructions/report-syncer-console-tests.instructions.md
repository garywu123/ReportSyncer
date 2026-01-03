---
applyTo: "Tests/ReportSyncer.Console.Tests/**/*.cs"
---

# ReportSyncer.Console.Tests — Copilot Instructions

## Scope
These are tests for Console Host behavior (CLI parsing, config precedence, exit code mapping, UI state reducers).
Do NOT test Core internals here.

## Preferred tools
- xUnit + FluentAssertions + Moq.

## Testing rules
- Prefer unit tests for:
  - CLI parsing & validation
  - appsettings.json defaults and CLI override precedence
  - exit code mapping given exception types / job results
  - progress event aggregation/state reducer logic (pure functions if possible)

- Avoid slow integration tests in Console.Tests. DB integration belongs in Core integration tests.

## Style
- Arrange/Act/Assert structure.
- Name tests as: `MethodOrScenario_WhenCondition_ShouldExpectedResult`.
- Assertions should be specific and readable (FluentAssertions).
