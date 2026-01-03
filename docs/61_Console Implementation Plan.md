# Console Implementation Plan（ReportSyncer.Console，MVP）

> 本文件是 `ReportSyncer.Console` 的**实施计划**（task list）。  
> Console 的行为/规则规范请以：`docs/60_Console Host Spec.md` 为准（唯一真相）。

## 0. 参考（必须对齐）

- `docs/60_Console Host Spec.md`
- `docs/20_ReportSyncer Backend Architecture.instruction.md`
- `docs/30_ReportSyncer Backend Detail Arch.md`

## 1. 范围与约束

- 本计划仅覆盖 **Console Host**（组合根 + 编排 + UI 渲染 + 退出码）。
- 不在 Console 实现任何业务逻辑（schema/safety/依赖计划/SQL/DML 决策均属于 Core）。
- 不引入 reporting system（Run Report / artifacts / history 服务均 out-of-scope）。

## 2. 任务列表（Console Host）

### Task 8.0 — Composition Root / Core Wiring（可运行的依赖注册与连接工厂）

目标：明确 Console Host 必须把 Core/Toolkit **真正 wiring 起来**，避免实现者只做 UI 但跑不起来，或在 Host 里硬编码连接字符串。

#### 8.0.1 Console 项目引用（最小集合）

- `ReportSyncer.Core`
- `DotNetToolkit.Logging`
- `DotNetToolkit.Database`
- Host 侧使用：
  - `Microsoft.Extensions.DependencyInjection`
  - `Microsoft.Extensions.Logging`
  - Serilog 相关包（Console/File + 自定义 in-memory sink）

> 代码核实：`ReportSyncer.Console/ReportSyncer.Console.csproj` 已包含对 `ReportSyncer.Core`、`DotNetToolkit.Logging`、`DotNetToolkit.Database` 的 ProjectReference。

#### 8.0.2 Core 服务注册清单（按当前代码库）

必须注册（按职责区分 root vs job-scope，避免“单例 TableRunner 固定连接串”的陷阱）：

- 配置层（Configuration）
  - `IConfigurationLoader` → `YamlConfigurationLoader`
  - `IConfigurationValidator` → `ConfigurationValidator`
  - `IConfigurationProvider` → `ConfigurationProvider`
- Schema 层
  - `ISchemaInspector` → `SqlServerSchemaInspector`
  - `ISchemaMapper` → `SchemaMapper`
  - `IDependencyResolver` → `DependencyResolver`
  - `ISchemaService` → `SchemaService`
- Preflight/Sync 层
  - `IPreFlightValidator` → `PreFlightValidator`
- SQL/写入层
  - `ISqlQueryBuilder` → `SqlServerQueryBuilder`
  - `IDataWriter` → `SqlDataWriter`
  - `IdentityInsertManager`
  - `IPermissionProfiler` → `SqlServerPermissionProfiler`
  - `ISafetyValidator` → `SafetyValidator`
- 观测/进度（Observability）
  - `IJobProgressReporter` → Host 实现（UI 渲染 + `JobProgressEvent`/`TableProgressEvent`）

job-scope 对象（每次 run 一个 job 时创建；用于绑定该 job 的 source/target 连接；可手工 new，不必注册到 root DI）：

- `IDbConnectionFactory sourceFactory`：用于 `TableRunner` 读取 source
- `IDbConnectionFactory targetFactory`：当前主链路未直接注入到 `TableRunner`；但 `WorkEstimator`（以及未来可能的 preflight gate）需要它
- `ITableRunner` → `TableRunner`（必须拿到 job-scope 的 source `IDbConnectionFactory`）
- `ISyncOrchestrator` → `SyncOrchestrator`（建议 job-scope/临时实例；只跑一个 job）

必须注册（建议 `Singleton`，Host 提供实现）：

- `DotNetToolkit.Logging.ILogService`：
  - Host 使用 Serilog 初始化后，注入 `SerilogLogService`（或等价适配器）。

#### 8.0.3 连接解析与 DB 工厂（MVP 规则，禁止硬编码）

代码核实（关键依赖形态）：

- `SqlServerSchemaInspector` 依赖：`Func<ConnectionConfig, IDbContext>`
- `SqlDataWriter` / `SqlServerPermissionProfiler` / `IdentityInsertManager` 依赖：`Func<string, IDbContext>`（按 connection name）
- `TableRunner` / `WorkEstimator` 依赖：`IDbConnectionFactory`（当前接口不带 name，意味着该 factory 必须是“已经绑定到某个连接字符串”的实例）

MVP 规则（实现者必须照做）：

1. **Host 先加载一次有效配置用于 wiring**
   - 调用 `IConfigurationProvider.LoadAndValidateAsync(configPath, overrides, ct)` 得到 `effectiveConfig`。
2. 建立映射：`connectionName -> connectionString`（来自 `effectiveConfig.Connections`）。
3. 构造两个工厂：
   - `Func<string, IDbContext> dbContextFactoryByName`
     - 用 name 查 connection string；
     - 使用 DotNetToolkit.Database 的 `DatabaseSettings`（ProviderName=`Microsoft.Data.SqlClient` + ConnectionString）创建：
       - `DbConnectionFactory`（需要 `IOptions<DatabaseSettings>`；可用 `Options.Create(...)`）
       - `DbContext`（还需要 `ILogger<DbContext>` + `IServiceProvider`；建议从 root provider 获取 logger，并把 root provider 作为 serviceProvider 传入）
   - `Func<ConnectionConfig, IDbContext> dbContextFactoryByConfig`
     - 直接使用 `ConnectionConfig.ConnectionString` 创建 `DbContext`（用于 `SqlServerSchemaInspector`）
4. **job 级 scope/工厂（为 TableRunner/WorkEstimator 绑定 source/target）**
   - 由于 `TableRunner`/`WorkEstimator` 依赖的 `IDbConnectionFactory` 不带 connection name，
     Host 必须为每个 job 创建绑定到该 job 的 source/target 连接字符串的 connection factory（或 job scope 容器）。

#### 8.0.4 Composition Root 结构（推荐：root provider + per-job 临时对象）

硬规则：**不要把 `TableRunner` / `SyncOrchestrator` 注册为全局单例**。  
原因：`TableRunner` 持有的 `_sourceConnectionFactory` 无法从 `TableExecutionContext.SourceConnectionName` 动态切换；若做成单例，会导致“只对某个 job 的 source 生效，其它 job 全错”。

推荐落地方式（MVP，可跑通、低复杂度）：

- root provider：注册所有“与具体 job 无关”的服务（配置/Schema/Preflight/SQL 写入/安全/日志/进度 reporter 等）。
- 每个 job 执行前：由 Host 根据 `effectiveConfig + job` 构造
  - `IDbConnectionFactory sourceFactory`（绑定到该 job 的 source 连接字符串）
  - `ITableRunner tableRunner = new TableRunner(sourceFactory, dataWriter, sqlQueryBuilder, progressReporter)`
  - `ISyncOrchestrator orchestrator = new SyncOrchestrator(configurationProvider, preFlightValidator, tableRunner, progressReporter)`
  - 然后调用 `orchestrator.RunJobAsync(configPath, jobName, overrides, ct)`。

> 备注：上述方式会导致 `SyncOrchestrator` 再次加载/校验 YAML（它内部调用 `IConfigurationProvider.LoadAndValidateAsync(...)`）。  
> 在当前 Core 结构下这是允许的 MVP 折中：**以可运行优先**；后续再通过 Core 侧引入“按 connection name 的连接工厂/作用域策略”消除重复加载与 job-scope 手工 new。

#### 8.0.5 验收信号（wiring 部分）

- 不需要改 Core 代码，Console 仅在 composition root/host 层完成 wiring。
- 多 job 运行时：不同 job 的 source/target 指向不同连接也能正确工作（不串线）。
- 不出现硬编码连接字符串；连接名解析来自 YAML 的 `connections`。
- `--dry-run` 或 `appsettings.json: run.dryRun` 能通过 `RuntimeOverrides.DryRun` 影响 Core 行为（table 结果出现 `SkippedDryRun`，且无 DML 证据）。

补充验收信号（端到端链路）：

- 能跑通“加载配置→preflight→执行/或 dry-run 返回”的完整链路（至少单 job）。

---

### Task 8.1 — Minimal CLI + `appsettings.json`（默认 `--config` / `--dry-run`，CLI 覆盖）

- 解析 CLI：`--config <path>`、`--dry-run [true|false]`；未知参数直接判定为 usage error。
- 加载可选 `appsettings.json`：
  - 支持 `run.jobConfigPath`、`run.dryRun` 作为默认值；
  - 其它 Host 行为配置（UI/logging/confirm）仅来自该文件，例如：
    - `ui.areaB.pageSize`（Area B 每页行数）
    - `logging.file.directory` / `logging.file.fileNamePrefix` / `logging.file.retentionCount`
- 优先级（硬规则）：CLI 覆盖 `appsettings.json`；YAML job config 与 app config 严格分离（不 merge）。
- 将 dry-run 作为 `RuntimeOverrides.DryRun` 传入 Core（推荐使用 `RuntimeOverridesFactory.FromCliOptions(dryRunFlag: ...)`），由 `IConfigurationProvider.LoadAndValidateAsync(path, overrides, ct)` 统一合并。
- 验收信号：
  - `--config` 与 `run.jobConfigPath` 都缺失 → exit `1`；
  - `--dry-run` 可显式覆盖 `run.dryRun`；
  - unknown args → exit `1`。

---

### Task 8.2 — 退出码对齐（Console ↔ Core）

- 退出码契约以 `docs/60_Console Host Spec.md` 的 **8. Exit Codes** 为准（本期采用：`0/1/2/3/4/5/6`）。
- Console 只做“结果/域异常 → 退出码”的翻译，不泄漏原始异常类别到用户契约里。

实现要求（硬规则）：

- 主流程必须能同时从以下两类信号决定退出码：
  - **CLI/Host**：参数解析与 usage 校验结果 → `2`。
  - **Core/Job**：preflight vs execution 的阶段信息 + `JobStatus/JobResult`（或异常类型）→ 映射到 `0/3/4/5/6/1`。
- `SchemaMismatchException` 必须映射为 `4 (PreflightFailed)`。
- `OperationCanceledException` 必须映射为 `6 (Cancelled)`。
- 未捕获/未分类异常必须兜底为 `1 (UnhandledFatal)`。

验收信号：

- 每个退出码都能稳定触发并映射到预期值，且打印一次用户可读的失败摘要：
  - `0` 成功（含 dry-run success / 全 skipped success）
  - `2` 参数错误
  - `3` 配置错误
  - `4` 预检失败
  - `5` 执行失败
  - `6` 取消
  - `1` 意外 fatal

---

### Task 8.3 — 日志落地（Console + JSON File）

- Serilog sinks：
  - Console（人类可读）
  - File（JSONL；每次进程启动创建新文件；默认 `logs/reportsyncer-<timestamp>.jsonl`）
- In-memory ring buffer（保存最近 N 行日志，用于 UI Area C 渲染）
- 日志级别与文件策略由 `appsettings.json` 控制（CLI 不提供对应参数）：
  - `logging.file.directory`（默认 `logs`）
  - `logging.file.fileNamePrefix`（默认 `reportsyncer`）
  - `logging.file.retentionCount`（默认 `20`；启动时清理旧文件，best-effort）
- 验收信号：一次运行同时产生控制台输出与 JSONL 文件；不生成任何 report artifacts。
  - Area C 固定显示最近 N 行日志，且 N 可通过 `appsettings.json: ui.areaC.maxLines` 配置（默认 200）。

补充要求（与 `docs/60_Console Host Spec.md` 对齐）：

- Non-goal：本期不集成 `RunLog` / `IRunLogWriter` / `FileSystemRunLogWriter`。
- 所有日志必须使用 message template（结构化字段），最低字段门槛：`JobId` / `Table` / `Kind` / `Phase`。

---

### Task 8.3.1 — Progress log throttling（避免 InProgress 刷爆日志）

目标：避免 `TableProgressEvent.Kind == InProgress` 等高频事件把 JSONL 文件写爆、并降低日志可读性。

硬规则：

- UI（Area B）允许按 `ui.refreshIntervalMs` 高频刷新。
- 文件日志（JSONL）对进度事件必须做节流（throttling / coalescing），至少满足以下之一：
  - **按时间**：同一 `JobId + Table + Phase` 最多每 N 秒记录一次（例如 2s~5s）；
  - **按百分比变化**：只有当进度百分比相比上次日志发生变化（或变化超过阈值）才记录；
  - **按行数变化**：rows processed 增量达到阈值才记录。

建议（非硬规则）：

- `Skipped` 不应记录为 `Warning`（除非业务定义“Skip = 异常”）；优先 `Information`。
- `Failed` 才应进入 `Error`/`Warning`（按严重性策略决定）。

验收信号：

- 对大表（长时间 InProgress）运行时，文件日志量与 UI 刷新解耦，不随事件频率线性增长。

---

### Task 8.4 — Console UI 与 Core 事件对接（仅展示层）

- 实现 Host 的 `IJobProgressReporter` 适配器，消费：
  - `JobProgressEvent`
  - `TableProgressEvent`
- 线程模型（硬规则，必须照 `docs/60_Console Host Spec.md` 执行）：
  - `Report(...)` 只做 enqueue（`Channel`/`ConcurrentQueue` 均可），不得渲染 UI、不得 `Console.WriteLine`、不得做 I/O
  - `Report(...)` 不得 throw（字段缺失降级丢弃并记录 warning）
  - UI loop 每 `ui.refreshIntervalMs` drain queue → 更新内存状态 → 重画屏幕
- Area C 日志区不依赖 Core 事件：使用 Serilog in-memory ring buffer（Host 自维护最近 N 行日志）。
- UI 必须遵守 `docs/60_Console Host Spec.md` 的 A/B/C/D 区域数据来源规则。
- 计划展示：仅展示 Core 产出的 `PreFlightResult.Schema.ExecutionPlan`（禁止 Host 自行计算/重排）。
- 验收信号：
  - Area B 能按事件更新每表状态/进度；
  - Area C 显示最近 N 行日志（来自 ring buffer）；
  - 执行顺序展示与 `ExecutionPlan` 一致。

补充：Area B 分页/过滤/键盘交互（仅展示层）

- 分页：Area B 显示第 `pageIndex` 页（每页 `ui.areaB.pageSize` 行）。
- 排序/过滤必须严格遵守 `docs/60_Console Host Spec.md` 7.3（不得自行定义另一套）。
- 验收信号：
  - 表数量 > 一页时可翻页；
  - `f/r/a` 切换立即生效；
  - 非交互式（stdin 重定向）时禁用键盘控制，固定 `a` + 第 1 页。

---

### Task 8.4.1 — Implement Area B paging/filter/sort exactly as Spec

目标：避免 Area B 出现“每个实现者一套”的分页/过滤/排序行为。

硬规则：

- page size：使用 `ui.areaB.pageSize`（默认 `20`；非法值回退 `20`）。
- refresh interval：使用 `ui.refreshIntervalMs`（默认 `250ms`；不得小于 `100ms`）。
- 排序：必须包含“Failed 永远置顶”的失败可见性规则，并遵守 Spec 中的排序 key（Status/Phase -> TableName -> InsertOrder）。
- 过滤：必须是 substring（非 regex）、case-insensitive（ordinal ignore-case），且作用于 `schema.table`。
- 顶部汇总：必须显示 `Total / Running / Completed / Failed / Skipped`。

最小单测（至少 2~3 个）：

- 过滤后页数变化：给定 N 张表与 pageSize，应用 filter 后，页数/可见表数量符合预期。
- 失败置顶：在混合状态集合中，`Failed` 表始终排在最前（不受其它排序影响）。
- 排序稳定性：同一状态组内按 TableName 字母序排序；必要时以 `InsertOrder` 保持稳定。

---

### Task 8.7 — 运行结束 Summary（可判读性，尤其非交互模式）

- 在进程结束前输出 Summary（至少 Console + 日志文件），字段要求见：`docs/60_Console Host Spec.md` 7.5。
- Summary 必须包含：总表数、Completed/Skipped/Failed、总耗时、失败表 Top N + 原因摘要、日志文件路径。
- 验收信号：
  - 非交互模式（stdin 重定向）下也能看到 Summary；
  - 仅凭 Summary + 日志文件路径即可快速判断本次运行结果。

---

### Task 8.7.1 — Render end-of-run screen exactly as Spec 7.1

目标：在交互式场景提供“运行结束页”，使操作者无需回滚日志也能读懂结局；在非交互场景依然输出同等信息。

硬规则：

- 运行结束后必须渲染/输出一个明确的 Summary block（对应 `docs/60_Console Host Spec.md` 7.1「运行结束布局」）。
- Summary block 必须包含：
  - `Config` / `Job` / `Dry-Run`
  - `ExitCode`
  - `Duration`
  - `Tables: Total / Completed / Skipped / Failed`
  - `Failed Top N`（建议 N=10）
  - `Logs`（本次 JSONL 文件路径）
- 非交互模式（stdin 重定向）下：不得依赖全屏 UI，必须以普通文本一次性输出上述 Summary。

验收信号：

- 完成后屏幕仍保留 Summary（不被后续刷新覆盖）。
- 操作者不查看日志文件，仅凭 Summary 也能判断：成功/失败/取消、失败表与原因、日志位置。

---
