# Task 5.3 — Step-by-Step Playbook（Work Estimator：DryRun 估算 + 大删除阈值输入）

> 交付对象：**Junior Executor AI**（低上下文、严格照做）
> 语言：说明中文；代码/注释英文
> 目标：实现 `WorkEstimator`，用 **纯 SELECT/COUNT** 计算：
>
> * `EstimatedRowsToInsert`
> * `EstimatedRowsToDelete`
> * `EstimatedDeletePct`（给 Safety Validator 的 `confirmLargeDeletePct` 使用）

---

## 1. 背景（Why）

### 1.1 当前现状

* Section 5 已有/将有：`Execution Contracts`（Task 5.1）与 `SQL Query Builder`（Task 5.2）。
* 现在需要一个 **Work Estimator**：DryRun 必须跑估算，但不能做任何 DML。
* 估算结果必须能支持 Safety 的“大删除百分比确认”规则（`confirmLargeDeletePct`）。

### 1.2 痛点/风险

* 如果估算失败却被当成 0 行继续跑，Safety 就是摆设。
* 如果估算用 DML 探测（比如试删一行再回滚），你会把“安全”做成“事故生成器”。
* 如果估算逻辑散落在 Runner/Writer 里，后续很难测试也难排障。

### 1.3 改造目标（必须可验证）

* Goal A：`EstimateAsync` 只执行 **COUNT/SELECT**，绝不产生任何数据修改。
* Goal B：估算产物包含 `RowsToDelete / RowsToInsert / DeletePct`，并携带可行动的 `Warnings`。
* Goal C：估算失败必须中断（抛异常或返回 fail），不得 silently fallback。
* Non-goals：本 Task 不实现 Safety 校验本体（那是 Task 5.5）；不实现真实 Delete/Insert（那是 5.8+）。

---

## 2. 影响范围（What/Where）

### 2.1 涉及文件与路径（必须列全）

必改/新增：

* `ReportSyncer.Core/Observability/WorkEstimator.cs`
* `ReportSyncer.Core/Observability/WorkEstimate.cs`
* `ReportSyncer.Core/Observability/EstimatedDeleteStats.cs`
* `ReportSyncer.Core/Observability/WorkEstimationException.cs`

可能要改（看你现有 Toolkit/DB 形状）：

* `ReportSyncer.Core/Sync/Sql/WhereClauseBuilder.cs`（若 Task 5.2 已实现，复用即可）
* `DotNetToolkit.Database/*`（仅当缺少 ExecuteScalar/参数绑定能力；优先写 extension，避免改 toolkit）

测试：

* `ReportSyncer.Core.Tests/Observability/WorkEstimatorTests.cs`
* `ReportSyncer.Core.Tests.Integration/Observability/WorkEstimatorIntegrationTests.cs`

### 2.2 关键类型（形状，禁止贴大段实现）

```csharp
// ReportSyncer.Core/Observability/WorkEstimate.cs
namespace ReportSyncer.Core.Observability;

public sealed record WorkEstimate(
    long EstimatedRowsToDelete,
    long EstimatedRowsToInsert,
    double? EstimatedDeletePct,
    EstimatedDeleteStats? DeleteStats,
    IReadOnlyList<string> Warnings);
```

```csharp
// ReportSyncer.Core/Observability/EstimatedDeleteStats.cs
namespace ReportSyncer.Core.Observability;

public sealed record EstimatedDeleteStats(
    long TargetTotalRows,
    long RowsToDelete,
    double? DeletePct);
```

```csharp
// ReportSyncer.Core/Observability/WorkEstimator.cs
namespace ReportSyncer.Core.Observability;

using ReportSyncer.Core.Sync.Contracts;

public sealed class WorkEstimator
{
    public Task<WorkEstimate> EstimateAsync(TableExecutionContext ctx, CancellationToken ct);
}
```

```csharp
// ReportSyncer.Core/Observability/WorkEstimationException.cs
namespace ReportSyncer.Core.Observability;

public sealed class WorkEstimationException : Exception
{
    public WorkEstimationException(string message, Exception? inner = null) : base(message, inner) {}
}
```

---

## 3. 行为变化（User-facing / API）

### 3.1 DryRun 行为

* DryRun 必须调用 `WorkEstimator.EstimateAsync`。
* 估算结果用于：

  * 报告给 UI/Host
  * 支撑 Safety 的 `confirmLargeDeletePct` 判定（即便 DryRun 也要算出来）

### 3.2 RealRun 行为

* RealRun 的 Preflight Gate（Task 5.6/5.5）会复用 WorkEstimate。
* WorkEstimator **不**做 Safety 拦截，只提供数据与 warnings。

---

## 4. 设计决策（Rules / Responsibilities）

### 4.1 职责边界（写死）

* `WorkEstimator`：只做估算（COUNT），不做 DML，不做权限探测，不做 safety。
* `WorkEstimate`：纯 DTO，稳定可序列化。
* `WorkEstimationException`：把失败变成可行动信息（Job/Table/Phase）。

### 4.2 规则定义（必须硬化成 If/Then）

* Rule 1：`If EstimateAsync is called then only SELECT COUNT(*) is executed`。
* Rule 2：`If ctx.PreSyncTargetDelete == false then EstimatedRowsToDelete = 0 and DeleteStats may be null`。
* Rule 3：`If ctx.PreSyncTargetDelete == true then TargetTotalRows must be computed (COUNT without filters)`。
* Rule 4：`If ctx.ContextColumnName != null then target-side delete-scope must include (ContextColumnName = ContextValue)`。
* Rule 5：`If any COUNT query fails then throw WorkEstimationException (do not return 0)`。

### 4.3 关于 Filter 的现实处理（别自欺欺人）

* 本 Task 只消费 `TableExecutionContext.Filters`（结构化谓词）。
* `Context Injection` **不属于 Filters**，但对 target delete-scope 必须追加一个 equals predicate。
* 不在这里做“filter 是否存在于 source/target schema”的校验（那是 Preflight/Mapper 的责任）。

---

## 5. Atomic Step-by-Step Implementation（How）

> 每步要求：**编译通过 + 相关单测通过**。

### Step 1 — 新增 DTO：WorkEstimate / EstimatedDeleteStats

**目标**

* 建立稳定 DTO，避免后续 UI/History 反复改字段。

**修改文件**

* `ReportSyncer.Core/Observability/WorkEstimate.cs`
* `ReportSyncer.Core/Observability/EstimatedDeleteStats.cs`

**实施要点**

1. 所有字段使用不可变 `record`。
2. `Warnings` 用 `IReadOnlyList<string>`。

**验收标准**

* `dotnet build` 通过。
* Unit test：`WorkEstimate` 能被构造（warnings 可空列表）。

---

### Step 2 — 新增异常类型：WorkEstimationException

**目标**

* 把估算失败变成“可行动”的错误（包含 Job/Table/Phase）。

**修改文件**

* `ReportSyncer.Core/Observability/WorkEstimationException.cs`

**实施要点**

1. Message 必须包含：`JobName`、`TargetSchema.TargetTable`、`Phase=Estimate`。
2. 保留 `innerException`。

**验收标准**

* Unit test：抛出异常时 message 包含上述字段。

---

### Step 3 — 定义 WorkEstimator 的依赖形态（不要硬编码连接）

**目标**

* 让 WorkEstimator 能执行 COUNT，但不自行解析 YAML/不自行创建 connection string。

**修改文件**

* `ReportSyncer.Core/Observability/WorkEstimator.cs`

**实施要点（按现有代码二选一，禁止发明第三种）**

**Option A（推荐，若你已有 DbContext Factory）**

* 构造函数注入：

```csharp
// English comments only
public WorkEstimator(IDbContextFactory dbContextFactory, IWhereClauseBuilder whereBuilder, ISqlIdentifier escaper) {}
```

* 通过 `ctx.SourceConnectionName / TargetConnectionName` 获取 source/target `IDbContext`。

**Option B（如果 TableExecutionContext 已经携带 IDbContext）**

* 不新增 factory，直接从 ctx 拿：

```csharp
public Task<WorkEstimate> EstimateAsync(TableExecutionContext ctx, CancellationToken ct)
{
    // ctx.SourceDb / ctx.TargetDb should be IDbContext
}
```

> 执行器必须先在 repo 里查清楚 `IDbContext` 获取方式，然后选 A 或 B。

**验收标准**

* `WorkEstimator` 可编译（先不实现逻辑）。

---

### Step 4 — 实现“参数化 COUNT SQL”组装（复用 Task 5.2 的 where builder）

**目标**

* 统一生成 `SELECT COUNT(1) FROM [schema].[table] WHERE ...`。

**修改文件**

* `WorkEstimator.cs`

**实施要点**

1. 必须复用 `SqlIdentifier`/`WhereClauseBuilder`（如果 Task 5.2 已有）。
2. 不允许把 Value 拼进 SQL。
3. 追加 target-side context predicate：

   * `if (ctx.ContextColumnName != null) predicates.Add(new FilterPredicate(ctx.ContextColumnName, Equals, ctx.ContextValue));`

**建议实现骨架（示意，注释英文）**

```csharp
private DbCommandSpec BuildCountSpec(string schema, string table, IReadOnlyList<FilterPredicate> predicates)
{
    // Build WHERE clause from structured predicates and parameter list
    var (whereSql, parameters) = _whereBuilder.Build(predicates);

    var sql = $"SELECT COUNT(1) FROM {_id.Qualify(schema, table)}";
    if (!string.IsNullOrWhiteSpace(whereSql))
        sql += " WHERE " + whereSql;

    return new DbCommandSpec(sql, parameters);
}
```

**验收标准**

* Unit test：生成 SQL 不包含任何字面值（例如 CustomerId=50 不应出现在 SQL 字符串中）。

---

### Step 5 — 实现 EstimateAsync：Source insert rows / Target delete rows / DeletePct

**目标**

* 计算：

  * `EstimatedRowsToInsert`：source count（使用 ctx.Filters）
  * `EstimatedRowsToDelete`：target count（仅当 `PreSyncTargetDelete=true`，并追加 context predicate）
  * `TargetTotalRows`：target total count（无 filters，仅当 `PreSyncTargetDelete=true`）
  * `EstimatedDeletePct`：`RowsToDelete / TargetTotalRows * 100`（total=0 时返回 null）

**修改文件**

* `WorkEstimator.cs`

**实施要点**

1. 所有查询都必须是 COUNT。
2. 查询失败：捕获异常并抛 `WorkEstimationException`。
3. Warnings 规则（最小集合）：

   * `PreSyncTargetDelete=true` 且 target predicates 为空：warning = "Delete scope has no predicates; this may delete the entire table."（英文）
   * `TargetTotalRows==0` 且 `RowsToDelete>0`：warning = "Inconsistent estimate: delete rows > 0 while total rows = 0."（英文）
4. 估算仅返回数字，不做决策。

**执行 COUNT 的方式（按你现有 DB 工具适配）**

* 如果 `IDbContext` 有类似：

  * `ExecuteScalarAsync<long>(sql, params)` 或 `ExecuteScalarAsync<T>(DbCommandSpec)`，直接用。
* 如果没有：

  * 在 `ReportSyncer.Core` 内新增一个 **最小 extension method** 把 `DbCommandSpec` 绑定到 `IDbCommandWrapper` 执行 scalar。
  * 不要改 toolkit（除非你确认 toolkit 还没稳定）。

**验收标准**

* Unit test：`PreSyncTargetDelete=false` 时 Delete 估算固定为 0，DeletePct 为 null。
* Unit test：`PreSyncTargetDelete=true` 时会计算 total 与 pct。

---

### Step 6 — 输出一致性与可观测性字段（最小）

**目标**

* WorkEstimator 输出必须稳定，错误信息可定位。

**修改文件**

* `WorkEstimator.cs`

**实施要点**

1. 抛异常 message 必须包含：

   * `JobName`
   * `TargetSchema.TargetTable`
   * `SourceSchema.SourceTable`
2. 如果你在 Core 内已有结构化 logger（Task 5.2/5.2.Observability 提到），在 Estimate start/end 打点：

   * Phase = `Estimate`
   * JobId / Table

**验收标准**

* Integration test：故意断开连接时，异常 message 包含 Job/Table。

---

## 6. 测试计划（必须）

### 6.1 新增/更新 Unit Tests 清单

> 每个测试必须写 Arrange / Act / Assert。

1. **`EstimateAsync_PreSyncTargetDeleteFalse_ReturnsInsertOnly`**

* Arrange：ctx.PreSyncTargetDelete=false；source table 任意；filters 任意
* Act：EstimateAsync
* Assert：EstimatedRowsToDelete==0；DeleteStats==null 或 DeletePct==null

2. **`EstimateAsync_PreSyncTargetDeleteTrue_ComputesDeleteAndPct`**

* Arrange：ctx.PreSyncTargetDelete=true；targetTotal>0；deleteRows>0
* Act
* Assert：DeleteStats.TargetTotalRows==expected；DeletePct==expected

3. **`EstimateAsync_AppendsContextPredicateToTargetDeleteScope`**

* Arrange：ctx.ContextColumnName="CustomerId"，ContextValue=50；filters 不含 CustomerId
* Act
* Assert：target delete COUNT 的参数列表包含 50（但 SQL 不包含 50 字面值）

4. **`EstimateAsync_WhenCountQueryFails_ThrowsWorkEstimationException`**

* Arrange：mock dbContext.ExecuteScalar throws
* Act/Assert：抛 WorkEstimationException 且 message 含 Job/Table/Phase

5. **`BuildCountSpec_IsParameterized_NoLiteralValues`**

* Arrange：predicates with numeric + datetime
* Act
* Assert：SQL 不包含这些值字符串

### 6.2 Integration Tests 清单（LocalDB / SQL Server）

> Integration 只验证：SQL 能执行、参数绑定正确、输出数字正确。不要在这里测 DML。

1. **`WorkEstimator_CountsSourceRows_WithDateFilter`**

* Arrange：source 表插入 3 行，日期过滤命中 2 行
* Act：Estimate
* Assert：EstimatedRowsToInsert==2

2. **`WorkEstimator_CountsTargetDeleteRows_WithContextCustomerId`**

* Arrange：target 表存在 CustomerId=50 的 5 行，CustomerId=60 的 5 行
* Act：Estimate with ContextValue=50
* Assert：EstimatedRowsToDelete==5

3. **`WorkEstimator_ComputesDeletePct_AgainstTargetTotal`**

* Arrange：target total=10，deleteRows=5
* Act
* Assert：DeletePct==50.0（允许 double 误差）

---

## 7. Integration Test SQL Script（可复用你 5.1/5.2 的 fixture）

> 如果你已经有更复杂的 fixture（含 Identity/FK），直接复用。这里给最小版本用于 WorkEstimator。

```sql
IF DB_ID('ReportSyncer_EstimatorDb') IS NOT NULL
BEGIN
    ALTER DATABASE ReportSyncer_EstimatorDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ReportSyncer_EstimatorDb;
END;
GO

CREATE DATABASE ReportSyncer_EstimatorDb;
GO

USE ReportSyncer_EstimatorDb;
GO

CREATE SCHEMA app AUTHORIZATION dbo;
CREATE SCHEMA rpt AUTHORIZATION dbo;
GO

CREATE TABLE app.SourceEvents
(
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    Payload NVARCHAR(100) NULL,
    CONSTRAINT PK_SourceEvents PRIMARY KEY (EventId)
);

CREATE TABLE rpt.TargetEvents
(
    CustomerId INT NOT NULL,
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    Payload NVARCHAR(100) NULL,
    CONSTRAINT PK_TargetEvents PRIMARY KEY (CustomerId, EventId)
);
GO

INSERT INTO app.SourceEvents(EventTime, Payload)
VALUES
('2025-12-20', 'a'),
('2025-12-25', 'b'),
('2025-12-26', 'c');

INSERT INTO rpt.TargetEvents(CustomerId, EventTime, Payload)
VALUES
(50, '2025-12-20', 'x'),
(50, '2025-12-21', 'y'),
(50, '2025-12-22', 'z'),
(60, '2025-12-20', 'u'),
(60, '2025-12-21', 'v');
GO
```

---

## 8. Executor 注意事项（防写歪）

* 不要在 WorkEstimator 里做任何 DML（哪怕事务回滚也不行）。
* 不要把 Safety 规则塞进估算里（估算只给数据，Safety 再做判断）。
* 不要硬编码 SQL Server 连接字符串来源，必须走既有 connection resolution（或 ctx 已携带）。
* 所有 SQL 必须参数化；标识符必须 escape（`[name]`）。
* 估算失败必须抛异常或返回 fail，总之不能继续跑 RealRun。
