---
description: Rules for tests in this repository
applyTo: "tests/**"
---

- Use xUnit for test cases, `FluentAssertions` for assertions, and a single mocking framework consistently (for example Moq).
- Prefer unit tests for pure logic (configuration, mapping, safety rules, orchestration control flow).
- Use integration tests only for code that talks to a real database or filesystem (schema inspection, data writer, work estimator).
- Follow the "Test Surfaces & Seams" matrix from the backend architecture docs when deciding where to attach tests.
- Keep tests deterministic and explicit about DB setup, test data, and expected side effects.
