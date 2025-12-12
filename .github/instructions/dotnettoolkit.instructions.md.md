---
description: Rules for DotNetToolkit infrastructure projects
applyTo: "src/DotNetToolkit.*/**"
---

- DotNetToolkit.* projects are domain-agnostic infrastructure libraries:
  - Do not reference any ReportSyncer.* project.
  - Public APIs must not mention ReportSyncer, AGV, sync jobs, YAML, or other app-specific concepts.

- Allowed dependencies:
  - Microsoft.Extensions.* libraries.
  - Microsoft.Data.SqlClient for database access.
  - No Entity Framework or Dapper in DotNetToolkit.Database.
  - No Serilog or other logging frameworks in DotNetToolkit.Logging beyond adapters.

- DotNetToolkit.Database:
  - Provide low-level abstractions like IDbConnectionFactory, IDbContext, IDbCommandWrapper, IDataMapper<T>.
  - Handle connection creation, command execution, basic mapping.
  - Do not implement sync-specific logic such as preSyncTargetAction, delete policies, identity insert strategy, or configuration parsing.

- DotNetToolkit.Logging:
  - Expose ILogService and adapters to concrete logging frameworks (for example Serilog).
  - Do not encode business-specific logging rules or ReportSyncer-specific log formats.
