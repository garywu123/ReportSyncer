# Playbook — Task 5.1：定义执行期核心数据结构（Execution Contracts）

## 1. 背景（Why）

### 1.1 当前现状

* Section 4 已经能产出：`SchemaSnapshot`、`SchemaMappingResult`、`ExecutionPlan`、`PreFlightResult`（或等价物）。
* Section 5 要把这些 **“预检产物”** 变成 **“执行期上下文”**，供后续 `TableRunner / DataWriter` 使用。
* 目前缺一个严格的执行期数据模型，导致后续 SQL Builder/Writer 很容易“临时猜 schema / 临时拼 SQL”。

### 1.2 痛点/风险

* 执行期如果还能“猜测/重排/自作主张”，你的 Preflight 就成了摆设。
* DTO 形状不稳定，后续（History/IPC/GUI）会被你自己坑死。
* “Host/UI 类型混入 Core” 会污染分层。

### 1.3 改造目标（必须可验证）

* Goal A：构造 `TableExecutionContext` 时，不需要 DB，也能表达 Delete/Insert/Estimate 所需字段。
* Goal B：执行期上下文只来源于 Preflight 产物（mapping/plan/snapshots/config），不允许二次推断。
* Non-goals：本 Task **不实现**任何 DML，不实现安全校验，不实现 writer。

---

## 2. 影响范围（What/Where）

### 2.1 涉及文件与路径（建议固定）

必改/必新增：

* `ReportSyncer.Core/Sync/Contracts/SyncPhase.cs`
* `ReportSyncer.Core/Sync/Contracts/TableExecutionContext.cs`
* `ReportSyncer.Core/Sync/Contracts/DeleteCommandContext.cs`
* `ReportSyncer.Core/Sync/Contracts/InsertCommandContext.cs`
* `ReportSyncer.Core/Sync/Contracts/FilterPredicate.cs`
* `ReportSyncer.Core/Sync/Contracts/DbCommandSpec.cs`  *(给 Task 5.2 用)*
* `ReportSyncer.Core/Sync/Contracts/CommandParameterSpec.cs`

测试：

* `ReportSyncer.Core.Tests/Sync/Contracts/TableExecutionContextTests.cs`
* `ReportSyncer.Core.Tests/Sync/Contracts/DbCommandSpecTests.cs`

### 2.2 牵扯到的关键类型（形状）

> 只给签名/字段，执行器不许脑补。

```csharp
// ReportSyncer.Core/Sync/Contracts/SyncPhase.cs
namespace ReportSyncer.Core.Sync.Contracts;

public enum SyncPhase
{
    Preflight,
    Estimate,
    Delete,
    Insert
}
```

```csharp
// ReportSyncer.Core/Sync/Contracts/FilterPredicate.cs
namespace ReportSyncer.Core.Sync.Contracts;

public sealed record FilterPredicate(
    string ColumnName,
    FilterOperator Operator,
    object? Value,
    object? Value2 = null);

public enum FilterOperator
{
    Equals,
    GreaterOrEqual,
    LessOrEqual,
    BetweenInclusive
}
```

```csharp
// ReportSyncer.Core/Sync/Contracts/TableExecutionContext.cs
namespace ReportSyncer.Core.Sync.Contracts;

public sealed record TableExecutionContext(
    Guid JobId,
    string JobName,
    string SourceConnectionName,
    string TargetConnectionName,
    DatabaseType SourceDbType,
    DatabaseType TargetDbType,
    string SourceSchema,
    string SourceTable,
    string TargetSchema,
    string TargetTable,
    bool DryRun,
    bool PreSyncTargetDelete,
    bool EnableIdentityInsert,
    string? ContextColumnName,
    object? ContextValue,
    IReadOnlyList<FilterPredicate> Filters,
    TableMapping TableMapping,
    ExecutionPlan ExecutionPlan,
    int BatchSize);
```

```csharp
// ReportSyncer.Core/Sync/Contracts/DeleteCommandContext.cs
namespace ReportSyncer.Core.Sync.Contracts;

public sealed record DeleteCommandContext(
    string TargetSchema,
    string TargetTable,
    IReadOnlyList<FilterPredicate> Filters);
```

```csharp
// ReportSyncer.Core/Sync/Contracts/InsertCommandContext.cs
namespace ReportSyncer.Core.Sync.Contracts;

public sealed record InsertCommandContext(
    string SourceSchema,
    string SourceTable,
    string TargetSchema,
    string TargetTable,
    IReadOnlyList<FilterPredicate> Filters,
    string? ContextColumnName,
    object? ContextValue,
    TableMapping TableMapping,
    bool EnableIdentityInsert,
    int BatchSize);
```

```csharp
// ReportSyncer.Core/Sync/Contracts/DbCommandSpec.cs
namespace ReportSyncer.Core.Sync.Contracts;

public sealed record DbCommandSpec(
    string Sql,
    IReadOnlyList<CommandParameterSpec> Parameters);

public sealed record CommandParameterSpec(
    string Name,
    object? Value,
    DbType? DbType = null);
```

> `DatabaseType`, `ExecutionPlan`, `TableMapping` 用你现有的（Section 3/4）类型；不要再造一套同名不同义的。

---

## 3. 设计决策（Rules / Responsibilities）

### 3.1 职责边界（写死）

* `TableExecutionContext`：**执行期唯一事实来源**（除了连接实例本身）。
* `DeleteCommandContext / InsertCommandContext`：把执行期语义压缩为 SQL 构建所需信息。
* `DbCommandSpec`：QueryBuilder 的产物，只描述 SQL + 参数，不碰连接、不执行。

### 3.2 规则定义（避免歧义）

* Rule 1：`TableExecutionContext` **必须由 Preflight 产物构造**，不得在运行期“再 Inspect Schema”。
* Rule 2：Filters 只允许结构化表达（`FilterPredicate`），不允许塞 “WHERE xxx” 字符串。
* Rule 3：Context Injection 信息（`ContextColumnName/ContextValue`）只能来自 job parameters + Preflight 判定（Application → Reporting），执行期只消费。
* Rule 4：Contracts **禁止引用** Host/UI（Console/WebApi/WPF）的任何类型。

---

## 4. Atomic Step-by-Step Implementation（How）

### Step 1 — 新增 Contracts 文件骨架

**目标**

* 建立 `SyncPhase/FilterPredicate/DbCommandSpec/*Context` 的最小可编译结构。

**修改文件**

* 新增上述 Contracts 文件

**实施要点**

1. 所有类型放在 `ReportSyncer.Core.Sync.Contracts` 下。
2. `record`/`enum` 一律不可变。
3. 先不要引入任何执行逻辑（不写方法体，最多写校验 helper 也别写复杂）。

**验收标准**

* `dotnet build` 通过。
* 单测项目引用这些类型不报错。

---

### Step 2 — 定义 `FilterOperator` 与 `FilterPredicate` 的硬约束

**目标**

* 把 “CustomerId/DateRange 等过滤” 变成可组合的结构化条件。

**修改文件**

* `FilterPredicate.cs`

**实施要点**

1. 支持最小集合：`Equals / GreaterOrEqual / LessOrEqual / BetweenInclusive`。
2. `BetweenInclusive` 使用 `Value` + `Value2`。
3. ColumnName 只存列名，不带 schema/table 前缀（避免重复拼接）。

**验收标准**

* Unit test：构造 `BetweenInclusive` 时 `Value2` 不为 null。
* Unit test：ColumnName 为空/空白时抛 `ArgumentException`（用 Guard）。

---

### Step 3 — 定义 `TableExecutionContext` 字段并固定含义

**目标**

* 一次性把执行所需输入定型，后续 Task 5.2/5.8 不得新增“临时字段”。

**修改文件**

* `TableExecutionContext.cs`

**实施要点**

1. 必须包含：JobId/JobName、Source/Target 表定位、DryRun、Filters、ContextInjection、BatchSize、Mapping、ExecutionPlan。
2. `ContextColumnName/ContextValue` 允许为 null（非注入场景）。
3. `PreSyncTargetDelete/EnableIdentityInsert` 从 table config 显式带入，执行期不猜。

**验收标准**

* Unit test：创建最小 `TableExecutionContext` 不需要 DB、不需要执行器实例。

---

### Step 4 — 定义 `DeleteCommandContext` / `InsertCommandContext`

**目标**

* 把 SQL Builder 的输入最小化，避免它拿到一堆不该碰的东西。

**修改文件**

* `DeleteCommandContext.cs`
* `InsertCommandContext.cs`

**实施要点**

1. Delete 只关心 target + filters。
2. Insert 需要 source/target + filters + context injection + mapping + identity/batch。
3. 这些 context 不包含 ConnectionString（防止日志泄露与职责污染）。

**验收标准**

* Unit test：InsertContext 能表达 “注入 CustomerId 列” 场景（ContextColumnName=CustomerId，ContextValue=50）。

---

### Step 5 — `DbCommandSpec` 与参数结构

**目标**

* 给 Task 5.2 一个统一输出结构，保证所有 SQL 都走参数化。

**修改文件**

* `DbCommandSpec.cs`

**实施要点**

1. `DbCommandSpec.Sql` 只允许包含参数占位符（`@p0` / `@CustomerId`）。
2. 参数用 `CommandParameterSpec` 表达，Value 允许 null（对应 `DBNull.Value` 转换交给执行层）。

**验收标准**

* Unit test：`DbCommandSpec` 可被序列化（如果你用 System.Text.Json），至少不崩。

---

---

# Playbook — Task 5.2：实现 SQL Query Builder（参数化 Delete/Select/Insert/Count）

## 1. 背景（Why）

### 1.1 当前现状

* 执行期已经有结构化 Filters 与上下文注入信息（来自 Task 5.1）。
* 现在缺一个 **严格参数化** 的 SQL 构建器，来生成 Delete/Select/Insert/Count 的 `DbCommandSpec`。

### 1.2 痛点/风险

* 人类最爱干的事：拼字符串 SQL，然后说“我会 sanitize 的”。你也知道这多蠢。
* QueryBuilder 如果偷偷生成 `DELETE FROM T`，Safety 就算写了也会被绕过。

### 1.3 改造目标（必须可验证）

* Goal A：所有生成的 SQL 都是参数化（无内嵌值）。
* Goal B：Filters 始终用 AND 组合，支持 CustomerId + DateRange 并存。
* Goal C：Context Injection 只影响 Insert 列清单，不触碰 Source。
* Non-goals：不实现 BulkCopy/TVP（如果你未来要，用策略接口另起 Task）。

---

## 2. 影响范围（What/Where）

### 2.1 文件与路径

必改/必新增：

* `ReportSyncer.Core/Sync/Sql/ISqlQueryBuilder.cs`
* `ReportSyncer.Core/Sync/Sql/SqlServerQueryBuilder.cs`
* `ReportSyncer.Core/Sync/Sql/SqlIdentifier.cs` *(可选，小工具，用来安全包 schema/table/column)*
* `ReportSyncer.Core/Sync/Sql/WhereClauseBuilder.cs` *(建议拆出来，免得类爆炸)*

测试：

* `ReportSyncer.Core.Tests/Sync/Sql/SqlServerQueryBuilderTests.cs`
* `ReportSyncer.Core.Tests.Integration/Sync/Sql/SqlServerQueryBuilderIntegrationTests.cs`

### 2.2 关键类型（形状）

```csharp
namespace ReportSyncer.Core.Sync.Sql;

using ReportSyncer.Core.Sync.Contracts;

public interface ISqlQueryBuilder
{
    DbCommandSpec BuildCountEstimate(TableExecutionContext ctx);

    DbCommandSpec BuildDelete(DeleteCommandContext ctx);

    DbCommandSpec BuildSelectSource(InsertCommandContext ctx);

    DbCommandSpec BuildInsertTarget(InsertCommandContext ctx);
}
```

---

## 3. 设计决策（Rules / Responsibilities）

### 3.1 职责边界

* `SqlServerQueryBuilder`：只负责 “把 context 翻译成 SQL+参数”。
* 不做 Safety（比如 allowAllDelete），**但** 要拒绝生成明显灾难的命令（见规则）。

### 3.2 规则定义（硬规则）

* Rule 1：禁止拼接值进 SQL（所有值都必须进参数）。
* Rule 2：WHERE 子句只来自 `FilterPredicate`，并且按顺序 AND 组合。
* Rule 3：没有任何 filters 时：

  * `BuildDelete` **必须失败**（抛 `ConfigurationException` 或返回 Result.Fail，按你 Core 错误模型一致性选一个，但要统一）。
  * `BuildCountEstimate` 可以允许全表 COUNT（估算用途）。
* Rule 4：Context Injection（Application → Reporting）：

  * Insert 的列清单包含 `ContextColumnName`，其值来自 `ContextValue` 参数。
  * SelectSource 永远只读，不包含注入列（注入列属于 target insert）。
* Rule 5：标识符（schema/table/column）必须安全引用：

  * 只允许 `[name]` 包裹，并拒绝含 `]` 的输入（或做严格替换）。
  * 不允许用户输入 `dbo.Table; DROP ...` 这种抽象艺术。

---

## 4. Atomic Step-by-Step Implementation（How）

### Step 1 — 新增 `ISqlQueryBuilder` 与 `SqlServerQueryBuilder` 空实现

**目标**

* 先把编译打通，方法都返回 `throw new NotImplementedException()`。

**修改文件**

* `ISqlQueryBuilder.cs`
* `SqlServerQueryBuilder.cs`

**验收标准**

* `dotnet build` 通过。

---

### Step 2 — 实现 `SqlIdentifier`（或等价 helper）

**目标**

* 统一处理 `[dbo].[Table]` / `[Column]` 的安全拼装。

**修改文件**

* `SqlIdentifier.cs`

**实施要点**

1. `EscapeIdentifier(string name)`：

   * if null/empty -> throw
   * if contains `]` -> throw（最简单且安全）
   * return $"[{name}]"
2. `Qualify(schema, table)` -> `"[schema].[table]"`

**验收标准**

* Unit tests 覆盖：含 `]` 的列名必须抛异常。

---

### Step 3 — 实现 `WhereClauseBuilder`：FilterPredicate → SQL + 参数

**目标**

* 把 filters 转为 `WHERE ... AND ...` 和参数列表。

**修改文件**

* `WhereClauseBuilder.cs`

**实施要点**

1. 参数命名策略固定：

   * 用递增：`@p0, @p1, ...`，避免列名带来的奇怪字符问题。
2. `Equals` → `[Col] = @pX`
3. `GreaterOrEqual` → `[Col] >= @pX`
4. `LessOrEqual` → `[Col] <= @pX`
5. `BetweenInclusive` → `[Col] >= @pX AND [Col] <= @pY`
6. 返回结构：

   * `string whereSql`（不含 WHERE 则为空字符串）
   * `List<CommandParameterSpec> parameters`

**验收标准**

* Unit test：两个 filters 组合必须用 `AND`。
* Unit test：SQL 字符串不得包含具体值（例如 “50”、“2025-01-01”）。

---

### Step 4 — 实现 `BuildCountEstimate(TableExecutionContext)`

**目标**

* 生成 `SELECT COUNT(1) ... WHERE ...`（允许无 filter）。

**实施要点**

1. SQL 模板：

   * `SELECT COUNT(1) FROM [schema].[table]` + whereSql
2. whereSql 来自 WhereClauseBuilder（ctx.Filters）
3. 参数直接带回 DbCommandSpec

**验收标准**

* Unit test：无 filter 时 SQL 不带 WHERE。
* Unit test：有 filter 时参数数量正确。

---

### Step 5 — 实现 `BuildDelete(DeleteCommandContext)`

**目标**

* 生成参数化 delete，**必须有 filter**。

**实施要点**

1. 如果 `Filters` 为空：抛 `ConfigurationException`（或你项目里等价配置错误）。
2. SQL 模板：

   * `DELETE FROM [schema].[table]` + whereSql
3. 不做 chunking（chunking 属于 DataWriter，不属于 Builder）。

**验收标准**

* Unit test：无 filter 必须失败。
* Unit test：SQL 不包含值字面量。

---

### Step 6 — 实现 `BuildSelectSource(InsertCommandContext)`

**目标**

* 生成 Source 侧只读 SELECT，用于后续 insert。

**实施要点**

1. 选择列来自 `TableMapping`：

   * 只选 “从 source 读取的列”（一对一映射中的 source 列）。
   * 常量/ContextColumn 不在 source select 中出现。
2. SQL 模板：

   * `SELECT [c1], [c2], ... FROM [schema].[table]` + whereSql
3. whereSql 使用 `ctx.Filters`（比如日期过滤从 source 限制行数）。

**验收标准**

* Unit test：ContextColumn 不出现在 SELECT 列清单。
* Unit test：列名全部 `[]` 包裹。

---

### Step 7 — 实现 `BuildInsertTarget(InsertCommandContext)`

**目标**

* 生成 target insert SQL（参数化），并支持 Context Injection。

**实施要点**

1. Insert 列清单来自 `TableMapping` 的 target 列（以及可选 context column）。
2. Values 子句使用参数占位（仍用 `@p0..`）。
3. Context Injection：

   * 若 `ContextColumnName != null`：插入列追加该列，对应值用参数（值=ctx.ContextValue）。
4. 批量插入策略（MVP）：

   * 先做单行 insert 的 spec（后续 DataWriter 可以循环/批处理）。
   * 不在这里实现 multi-row values 拼装（容易超长且没收益）。

**验收标准**

* Unit test：Application→Reporting 场景 insert 列多一列，参数多一个。
* Unit test：所有值通过参数表达。

---

# 5. 测试计划（必须）

下面把 **Task 5.1 + 5.2** 需要的测试一次列全。你要的 “Unit + Integration” 都在这里，别再靠祈祷保证质量了。

## 5.1 Unit Test Cases（建议 xUnit）

### Contracts（Task 5.1）

1. `TableExecutionContext_WhenCreated_HasAllRequiredFields`

* Arrange：构造最小 mapping/plan/filters
* Act：new `TableExecutionContext(...)`
* Assert：字段非 null，Filters 数量正确

2. `FilterPredicate_WhenColumnNameBlank_ThrowsArgumentException`

* Arrange：`ColumnName = " "`
* Act/Assert：抛 `ArgumentException`

3. `FilterPredicate_BetweenInclusive_RequiresValue2`

* Arrange：`Operator=BetweenInclusive, Value2=null`
* Act/Assert：抛异常（或 Guard Fail）

4. `DbCommandSpec_DoesNotMutateParametersCollection`

* Arrange：传入 list
* Act：外部 list 修改
* Assert：Spec 内参数不应被外部修改（用 `ToArray()`/`AsReadOnly()` 固化）

### SQL Builder（Task 5.2）

5. `WhereClauseBuilder_WithTwoPredicates_UsesAndAndCreatesTwoParameters`

* Arrange：CustomerId=50, OrderDate>=X
* Act：Build
* Assert：SQL contains `AND`；参数 2 个；SQL 不含 `50`

6. `BuildDelete_WithNoFilters_ThrowsConfigurationException`

* Arrange：Filters empty
* Act/Assert：抛配置异常

7. `BuildCountEstimate_WithNoFilters_NoWhereClause`

* Assert：SQL 不包含 `WHERE`

8. `BuildSelectSource_DoesNotIncludeContextColumn`

* Arrange：InsertCommandContext.ContextColumnName="CustomerId"
* Assert：SELECT 列不含 CustomerId

9. `BuildInsertTarget_WithContextInjection_AppendsColumnAndParameter`

* Assert：insert 列数 = mapping target columns + 1；参数数一致

10. `SqlIdentifier_WhenNameContainsClosingBracket_Throws`

* Arrange：`"Bad]Name"`
* Assert：抛异常

## 5.2 Integration Test Cases（建议 LocalDB 或 Test SQL Server）

> Integration 的目标不是测 DML（那是 5.8+），而是确认：Builder 输出的 SQL **在真实 SQL Server 上能编译/执行**，并且参数绑定正确。

1. `QueryBuilder_CountEstimate_ExecutesSuccessfully`

* Arrange：用脚本建表并插入数据
* Act：执行 `BuildCountEstimate` 的 SQL（用 `IDbContext` 执行 scalar）
* Assert：返回 count == 预期

2. `QueryBuilder_Delete_WithFilter_DeletesExpectedRows_InTransactionRollback`

* Arrange：开启事务，执行 delete
* Act：执行 delete 后再 count
* Assert：count 变化符合预期；最后 rollback（不污染数据）

3. `QueryBuilder_SelectSource_WithDateFilter_ReturnsExpectedRows`

* Arrange：插入不同日期行
* Act：执行 select
* Assert：行数正确

4. `QueryBuilder_InsertTarget_WithContextInjection_InsertsRow_InTransactionRollback`

* Arrange：事务内执行 insert
* Act：count target where CustomerId=50
* Assert：新增行存在；rollback

---

# 6. Integration Test SQL Script（更复杂一点，含 FK + Identity + DateFilter + Context 注入）

下面脚本会创建 **两个数据库**：`ReportSyncer_SourceApp`（无 CustomerId）与 `ReportSyncer_TargetRpt`（有 CustomerId + 复合 FK）。它足够让你的 Builder/后续 Writer 测到痛点（identity、FK、过滤）。
**注意**：这脚本不会替你实现安全策略，它只是测试夹具。你要真在生产跑 `DROP DATABASE`，那属于人类行为艺术。

```sql
/* ============================================================
   Integration Test Fixture for ReportSyncer
   - Source: Application DB (no CustomerId)
   - Target: Reporting DB (CustomerId required)
   ============================================================ */

-- Cleanup (careful)
IF DB_ID('ReportSyncer_SourceApp') IS NOT NULL
BEGIN
    ALTER DATABASE ReportSyncer_SourceApp SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ReportSyncer_SourceApp;
END;

IF DB_ID('ReportSyncer_TargetRpt') IS NOT NULL
BEGIN
    ALTER DATABASE ReportSyncer_TargetRpt SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ReportSyncer_TargetRpt;
END;
GO

CREATE DATABASE ReportSyncer_SourceApp;
CREATE DATABASE ReportSyncer_TargetRpt;
GO

/* -----------------------------
   Source (Application)
   ----------------------------- */
USE ReportSyncer_SourceApp;
GO

CREATE SCHEMA app AUTHORIZATION dbo;
GO

CREATE TABLE app.Products
(
    ProductId INT IDENTITY(1,1) NOT NULL,
    Name NVARCHAR(80) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_SourceProducts_IsActive DEFAULT(1),
    CONSTRAINT PK_SourceProducts PRIMARY KEY (ProductId)
);

CREATE TABLE app.Orders
(
    OrderId BIGINT IDENTITY(1000,1) NOT NULL,
    OrderDate DATETIME2(0) NOT NULL,
    ProductId INT NOT NULL,
    Qty INT NOT NULL,
    CONSTRAINT PK_SourceOrders PRIMARY KEY (OrderId),
    CONSTRAINT FK_SourceOrders_Product FOREIGN KEY (ProductId) REFERENCES app.Products(ProductId)
);

CREATE TABLE app.OrderLines
(
    OrderLineId INT IDENTITY(1,1) NOT NULL,
    OrderId BIGINT NOT NULL,
    LineNo INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CONSTRAINT PK_SourceOrderLines PRIMARY KEY (OrderLineId),
    CONSTRAINT UQ_SourceOrderLines_Order_Line UNIQUE (OrderId, LineNo),
    CONSTRAINT FK_SourceOrderLines_Order FOREIGN KEY (OrderId) REFERENCES app.Orders(OrderId)
);

CREATE TABLE app.HistoryEvents
(
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    EventType NVARCHAR(40) NOT NULL,
    Payload NVARCHAR(200) NULL,
    CONSTRAINT PK_SourceHistoryEvents PRIMARY KEY (EventId)
);

INSERT INTO app.Products(Name) VALUES (N'Forklift'), (N'Charger'), (N'Sensor');
INSERT INTO app.Orders(OrderDate, ProductId, Qty)
VALUES
('2025-12-20', 1, 10),
('2025-12-25', 2, 5),
('2025-12-26', 3, 2);

INSERT INTO app.OrderLines(OrderId, LineNo, Amount)
VALUES
(1000, 1, 100.00),
(1000, 2, 50.00),
(1001, 1, 25.00);

INSERT INTO app.HistoryEvents(EventTime, EventType, Payload)
VALUES
('2025-12-20', N'BOOT', N'a'),
('2025-12-25', N'ALERT', N'b'),
('2025-12-26', N'ALERT', N'c');
GO


/* -----------------------------
   Target (Reporting)
   - Requires CustomerId
   - Identity columns preserved via IDENTITY_INSERT (later tasks)
   - FK uses (CustomerId, ParentId) style
   ----------------------------- */
USE ReportSyncer_TargetRpt;
GO

CREATE SCHEMA rpt AUTHORIZATION dbo;
GO

CREATE TABLE rpt.DimProduct
(
    CustomerId INT NOT NULL,
    ProductId INT IDENTITY(1,1) NOT NULL,
    Name NVARCHAR(80) NOT NULL,
    IsActive BIT NOT NULL,
    CONSTRAINT PK_DimProduct PRIMARY KEY (CustomerId, ProductId)
);

CREATE TABLE rpt.FactOrder
(
    CustomerId INT NOT NULL,
    OrderId BIGINT IDENTITY(1000,1) NOT NULL,
    OrderDate DATETIME2(0) NOT NULL,
    ProductId INT NOT NULL,
    Qty INT NOT NULL,
    CONSTRAINT PK_FactOrder PRIMARY KEY (CustomerId, OrderId),
    CONSTRAINT FK_FactOrder_DimProduct
        FOREIGN KEY (CustomerId, ProductId)
        REFERENCES rpt.DimProduct(CustomerId, ProductId)
);

CREATE TABLE rpt.FactOrderLine
(
    CustomerId INT NOT NULL,
    OrderLineId INT IDENTITY(1,1) NOT NULL,
    OrderId BIGINT NOT NULL,
    LineNo INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CONSTRAINT PK_FactOrderLine PRIMARY KEY (CustomerId, OrderLineId),
    CONSTRAINT UQ_FactOrderLine_Order_Line UNIQUE (CustomerId, OrderId, LineNo),
    CONSTRAINT FK_FactOrderLine_FactOrder
        FOREIGN KEY (CustomerId, OrderId)
        REFERENCES rpt.FactOrder(CustomerId, OrderId)
);

CREATE TABLE rpt.HistoryEvents
(
    CustomerId INT NOT NULL,
    EventId INT IDENTITY(1,1) NOT NULL,
    EventTime DATETIME2(0) NOT NULL,
    EventType NVARCHAR(40) NOT NULL,
    Payload NVARCHAR(200) NULL,
    CONSTRAINT PK_RptHistoryEvents PRIMARY KEY (CustomerId, EventId)
);

-- Seed some existing target data for two customers
SET IDENTITY_INSERT rpt.DimProduct ON;
INSERT INTO rpt.DimProduct(CustomerId, ProductId, Name, IsActive)
VALUES
(50, 1, N'Forklift_OLD', 1),
(50, 2, N'Charger_OLD', 1),
(60, 1, N'Forklift_OTHER', 1);
SET IDENTITY_INSERT rpt.DimProduct OFF;

SET IDENTITY_INSERT rpt.FactOrder ON;
INSERT INTO rpt.FactOrder(CustomerId, OrderId, OrderDate, ProductId, Qty)
VALUES
(50, 1000, '2025-12-20', 1, 999),
(60, 1000, '2025-12-20', 1, 111);
SET IDENTITY_INSERT rpt.FactOrder OFF;

SET IDENTITY_INSERT rpt.FactOrderLine ON;
INSERT INTO rpt.FactOrderLine(CustomerId, OrderLineId, OrderId, LineNo, Amount)
VALUES
(50, 1, 1000, 1, 999.00),
(60, 1, 1000, 1, 111.00);
SET IDENTITY_INSERT rpt.FactOrderLine OFF;

SET IDENTITY_INSERT rpt.HistoryEvents ON;
INSERT INTO rpt.HistoryEvents(CustomerId, EventId, EventTime, EventType, Payload)
VALUES
(50, 1, '2025-12-20', N'ALERT', N'old'),
(60, 1, '2025-12-20', N'ALERT', N'other');
SET IDENTITY_INSERT rpt.HistoryEvents OFF;
GO
```
