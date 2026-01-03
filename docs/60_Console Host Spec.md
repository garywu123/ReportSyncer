# ReportSyncer.Console — Console Host 规范（V1，合并版）

> 本文件是 `ReportSyncer.Console` 的**唯一真相**（Single Source of Truth）。  
> Console 只负责 **Host（组合根 + 编排 + 渲染）**；所有业务逻辑必须在 `ReportSyncer.Core`。
>
> Console 的实施计划（task list）见：`docs/61_Console Implementation Plan.md`。

## 0. 需对齐的权威文档（不得偏离）

- `docs/20_ReportSyncer Backend Architecture.instruction.md`（分层/依赖/Host 边界）
- `docs/30_ReportSyncer Backend Detail Arch.md`（退出码、取消、dry-run/preflight 不变式、进度/日志事件契约）

---

## 1. 范围、当前决策、非目标

### 1.1 当前决策（不讨论）

- **仅 Console**：Electron/GUI 已放弃。
- **CLI 极简**：必须支持
  - `--config <path>`
  - `--dry-run`
- **无 reporting system**：不做报告/产物系统，只需要日志输出（见第 5 节）。
- **新增（Host 级 app config）**：`appsettings.json` 可以提供默认的：
  - YAML job config 路径（即 `--config` 的默认值）
  - 是否 dry-run（即 `--dry-run` 的默认值）
  - **CLI 优先级更高**：命令行参数可覆盖 `appsettings.json` 的定义。

### 1.2 非目标（明确排除）

- 重新引入 GUI。
- 设计复杂命令/子命令体系。
- 引入 Run Report / report artifacts 管线。
- 重构/改造 `ReportSyncer.Core` 架构。

---

## 2. Host 边界（职责规则）

### 2.1 Console Host 必须做（MUST）

- 解析 CLI 参数并做 usage 校验。
- 读取可选的 Host 配置（`appsettings.json`），应用默认值。
- 作为 **Composition Root**：
  - 构建 DI 容器；
  - 配置 Serilog sinks；
  - 组装并调用 Core 的入口服务。
- 订阅/消费 Core 的可观测性输出（进度事件）与日志输出（`ILogService`/Serilog），并将其渲染到 Console UI。
- 将 Core 的**域异常（domain exceptions）**映射为标准退出码（见第 8 节）。

### 2.2 Console Host 严禁做（MUST NOT）

- 在 Host 内实现或重复实现业务逻辑：
  - schema inspection、schema mapping、依赖解析、safety rules、SQL 生成、DML 决策等。
- 计算执行顺序/依赖计划：
  - Console 只能**展示** Core 产出的 `ExecutionPlan`（来自 `PreFlightResult.Schema.ExecutionPlan`），不得自行重排。
- 发明新的“Host 专用异常分类”：
  - Host 只根据 Core 的域异常分类来决定退出码与用户提示。

---

## 3. 输入与配置

### 3.1 CLI（极简）

支持的参数（V1）：

```bash
reportsyncer [--config <path>] [--dry-run [true|false]]
```

规则：

- 未知参数一律视为 usage error，退出码 **2**。
- `--config <path>`
  - 指向 **job YAML**（由 `ReportSyncer.Core` 使用 YamlDotNet 加载与校验）。
  - 若缺失且 `appsettings.json` 也未提供默认值，则为 usage error（退出码 **2**）。
- `--dry-run [true|false]`
  - 若 `--dry-run` 不带值，等价于 `true`。
  - 若不提供该参数，则使用 `appsettings.json` 的默认值（见 3.2）。

> 说明：这里用同一个 `--dry-run` 支持显式 `true/false`，以满足“CLI 可覆盖配置文件”的需求，同时保持 CLI 仍然极简。
> 注意：CLI 采用 `Spectre.Console.Cli` 框架

### 3.2 Host 应用配置（`appsettings.json`，可选）

文件名：`appsettings.json`（固定名）

定位/加载规则：

- 文件**可缺省**：不存在则全部使用默认值。
- 该配置仅控制 **Host 行为与默认输入**（UI/logging/确认策略/默认 configPath/dryRun）。
- 该配置与 job YAML **严格分离**：
  - 不合并；
  - 不复制 job YAML 的内容（只允许保存 YAML 的路径与 dry-run 默认值）。

最小 schema（形状 + 默认值）：

```json
{
  "run": {
    "jobConfigPath": null,
    "dryRun": false
  },
  "ui": {
    "refreshIntervalMs": 250,
    "showAdvancedPanels": false,
    "areaB": {
      "pageSize": 20
    },
    "areaC": {
      "maxLines": 20
    }
  },
  "logging": {
    "level": "Information",
    "progressThrottleMs": 5000,
    "file": {
      "enabled": true,
      "directory": "logs",
      "fileNamePrefix": "reportsyncer",
      "retentionCount": 20
    }
  },
  "execution": {
    "confirmEachJob": true
  }
}
```

合并/优先级规则（硬规则）：

- `run.jobConfigPath`（YAML 路径）
  - `CLI --config` > `appsettings.json: run.jobConfigPath` >（缺失则 usage error，退出码 1）
- `run.dryRun`
  - `CLI --dry-run <value>` > `appsettings.json: run.dryRun` > 默认 `false`
- 其余 Host 行为配置（UI/Logging/Confirm）：
  - 仅来自 `appsettings.json`（CLI 不提供对应参数）

UI 默认值规则（硬规则）：

- `ui.areaB.pageSize`：
  - 必须为正整数；非法值回退为 `20`。
- `ui.areaC.maxLines`：
  - 必须为正整数；非法值回退为 `20`。

交互性默认值规则：

- `execution.confirmEachJob`：
  - 交互式（stdin 未重定向）默认 `true`
  - 非交互式默认 `false`

---


## 3.3 Tooling note：`Spectre.Console` / `Spectre.Console.Cli`（可选）

说明：`Spectre.Console` 是一个成熟的控制台渲染与 CLI 框架生态，若实现者选择在 Console Host 中使用该库，建议遵循本小节的约定以保证与本 Spec 的一致性。

- 可选性：使用 `Spectre.Console` / `Spectre.Console.Cli` 完全可接受，但不是强制依赖。Host 的行为契约（CLI 参数、appsettings 合并、UI 刷新/线程模型、退出码映射）必须遵守本 Spec。

- 建议用途：
  - `Spectre.Console.Cli`：用于命令/参数解析（建议用于实现一致的 help/usage 文档与参数验证）。
  - `Spectre.Console`（`AnsiConsole`、Tables、Live` 等）：可用于 Area A/B/C/D 的渲染，尤其在交互式 UI 或彩色/表格输出时风格统一。

- 推荐实践（与 Spec 的对齐点）：
  - CLI 解析：通过 `Spectre.Console.Cli` 将 `--config`、`--dry-run` 等参数映射到一个 `Settings` 类，然后把解析结果转换为本 Spec 要求的优先级（CLI > appsettings.json > defaults），并构造 `RuntimeOverrides`。
  - 非交互探测：`Console.IsInputRedirected` 或 `AnsiConsole.Profile.Capabilities` 可用于判断非交互模式，进而禁用键盘控制并固定 `a` + 第 1 页。
  - UI 渲染循环：即使使用 `Spectre.Console.Live` 或 `AnsiConsole` 的 `Status`/`Live`，也必须遵守 7.4 中关于单一 UI loop 的硬规则；`IJobProgressReporter.Report(...)` 仍不得直接写入 Console，而应把事件放入队列，由 UI loop 在安全时机 drain 并渲染。
  - 日志与渲染分离：尽管 `Spectre.Console` 支持直接写富文本到屏幕，所有日志持久化仍应走 `ILogService`（Serilog）；Area C 的滚动日志区应从 Serilog 的 ring buffer 读取，而不是直接使用 `Spectre` 的即时输出作为持久化来源。

- 实现注意事项与陷阱：
  - 避免在 `IJobProgressReporter.Report(...)` 或事件处理路径中执行任何阻塞/长耗时的 `Spectre` 渲染调用；这些调用必须集中在单独的 UI loop。
  - 如果使用 `Spectre.Console.Cli` 的验证特性（validators），确保其返回的 usage error 与本 Spec 的退出码策略一致（参见第 8 节）。
  - Spectre 的渲染会跟控制台缓冲区交互，复杂的全屏重画需要在实现时仔细测试不同终端/Windows 控制台、PowerShell、CI（无 ANSICON）场景下的行为。

- 示例（简要，供实现参考）：

```csharp
// Settings 类（Spectre.Console.Cli）
public class RunSettings : CommandSettings
{
    [CommandOption("--config <PATH>")]
    public string ConfigPath { get; init; }

    [CommandOption("--dry-run")] // 支持无参数时作为 true
    public bool? DryRun { get; init; }
}

// 在命令执行入口：
// 1) 读取 appsettings.json -> hostConfig
// 2) 将 RunSettings 映射并覆盖 hostConfig（CLI 优先）
// 3) 构造 RuntimeOverrides 并传给 IConfigurationProvider.LoadAndValidateAsync(...)
```

- 验收要点（若采用 Spectre）：
  - CLI help/usage 与本 Spec 的参数集合一致（仅 `--config`、`--dry-run`）；未知参数被视为 usage error 并按 Spec 返回对应退出码。
  - 非交互模式行为（禁用键盘控制、输出 Summary）与本 Spec 一致。
  - UI loop 与 `IJobProgressReporter` 的线程模型、日志节流、以及 Area C 的日志来源与本 Spec 对齐。

总结：若选用 `Spectre.Console` / `Spectre.Console.Cli`，可获得一致的命令与渲染体验，并利用其完整文档与样例，但必须严格按本 Spec 的硬规则实现交互/渲染/退出码/日志策略以保持行为可预测与可测试。

---


## 4. Composition Root（DI 组装职责）

### 4.1 必须从 DI 获取的 Core 服务

- `IConfigurationProvider`：加载与校验 YAML（必须使用 Core 的 validated path）
- `IPreFlightValidator`：执行 pre-flight 并产出 `PreFlightResult`（其中 `Schema.ExecutionPlan` 为表执行顺序；用于 UI 展示计划）

执行器构造规则（硬规则）：

- **不要把 `ISyncOrchestrator` / `ITableRunner` / `TableRunner` 注册为全局单例**。
- 每次执行一个 job 时，Host 必须创建一个 **job-scope 的 `ISyncOrchestrator` 实例**（绑定该 job 的 source/target DB 工厂），再调用 `RunJobAsync(...)`。

### 4.2 Host 提供的适配器

- `IJobProgressReporter`（Host 实现）：
  - 消费 Core 的 `JobProgressEvent` / `TableProgressEvent`
  - 更新 Console UI（第 7 节的 A/B/C/D 区域）
  - 该适配器不得引入新的“报告 schema”，只做展示/转发

### 4.3 依赖注入与连接解析（MVP 必写规则，避免“容器能 build 但跑不起来”）

代码核实（当前 Core 真实依赖形态）：

- 多个组件需要按 **connection name** 或按 **job** 绑定的 DB 依赖：
  - `SqlDataWriter` / `SqlServerPermissionProfiler` / `IdentityInsertManager` 使用 `Func<string, IDbContext>`（按 connection name 创建 `IDbContext`）
  - `TableRunner` / `WorkEstimator` 使用 `IDbConnectionFactory`（当前接口不携带 connection name）
- 因此 Host 必须提供一个“连接名 → 连接字符串”的解析能力，并据此构造：
  - `Func<string, IDbContext>`（面向按 name 的组件）
  - job 级别的 `IDbConnectionFactory`（面向 `TableRunner`/`WorkEstimator`）

MVP 实施规则（推荐做法）：

1. Host 先通过 `IConfigurationProvider.LoadAndValidateAsync(configPath, overrides, ct)` 得到 **effectiveConfig**（这是唯一允许的“拿到已校验配置”的方式）。
2. Host 从 `effectiveConfig.Connections` 建立不可变映射：`connectionName -> connectionString`（或封装为 resolver）。
3. 对每个要执行的 job（从 effectiveConfig 选择/枚举）：
   - 解析 `job.SourceConnection` / `job.TargetConnection` 到连接字符串；
   - 构造该 job 专属的 `IDbConnectionFactory`（source/target）；
   - 构造 `Func<string, IDbContext>`，使其能用 connection name 创建对应 `IDbContext`；
   - 将上述工厂注入到 `TableRunner` / `SqlDataWriter` / `SchemaInspector` / `PermissionProfiler` / `WorkEstimator` 等依赖中。

job-scope 组装规则（避免“跑得起来但串线”）：

- `TableRunner` 持有的 `_sourceConnectionFactory` 无法从 `TableExecutionContext.SourceConnectionName` 动态切换；因此必须在 job-scope 里把 source factory **绑定到当前 job 的 source**。
- 推荐做法（MVP）：root DI 仅负责解析“与具体 job 无关”的 Core 服务；每个 job 执行前由 Host 手工 `new TableRunner(...)`、`new SyncOrchestrator(...)`（依赖从 root 取，连接工厂从 job 绑定）。

> 说明：当前 `ISyncOrchestrator.RunJobAsync(...)` 会自行再次加载配置（它也接受 `overrides`），
> 因此上述第 1 步属于 Host 的“为 DI 绑定连接”目的，可能导致一次运行里 YAML 被加载两次。
> 这是当前代码结构下可跑通的 MVP 折中；后续可以通过 Core 提供更清晰的连接工厂/作用域策略来消除重复加载。

---

## 5. 可观测性（Logging-first；无报告产物）

硬规则：

- 仅做 **结构化日志 + 实时 UI 展示**。
- 不生成 Run Report / report artifacts 文件。
- Console Host 配置 Serilog sinks：
  - **Console sink**：人类可读（可与 UI Area C 结合）
  - **File sink**：JSONL（逐行 JSON），**每次进程启动创建新文件**（文件名带 timestamp），目录/前缀/保留策略来自 `logging.file.*`
  - **In-memory sink（ring buffer）**：保存最近 N 条日志，供 UI Area C 渲染（Host 自维护）

### 5.0 Logging 辊界与质量门槛（Normative）

Non-goal（本期明确不做）：

- Console Host **不集成** Core 中的 RunLog 体系：`RunLog` / `IRunLogWriter` / `FileSystemRunLogWriter`（除非后续 task 明确要求）。
  - 理由：避免出现“两套日志系统”并长期并行。

Single source of logging（唯一日志来源）：

- Console Host 的运行日志 **仅**通过 `DotNetToolkit.Logging.ILogService`（Serilog 实现）输出到：
  - Console（人读）
  - File（工程排障；建议 JSON structured log / JSONL）

Structured logging（结构化要求）：

- Host 与 Core 的日志消息必须使用 **message template**（结构化字段），不得以“拼字符串”为主。
  - 例如：不要 `$"Job {evt.JobId} {evt.Kind} ({evt.Phase})"`
  - 要用：`"Job {JobId} {Kind} ({Phase})"`
- 最低字段门槛（建议作为日志查询维度）：`JobId` / `Table` / `Kind` / `Phase`。

Progress logging（进度日志节流）：

- `InProgress` 类事件可能高频（rows processed/percent 变化频繁）；若直接逐事件落文件，会导致日志膨胀与噪音。
- Console UI 可以高频刷新，但 **文件日志必须有节流策略**（节流规则见 `docs/61_Console Implementation Plan.md` 对应 task）。

补充（Host 必须实现，默认 time-based，可配置）：

- 默认节流策略：**time-based**，阈值由 `appsettings.json: logging.progressThrottleMs` 控制，默认值为 `5000ms`（5s）。
  - Host 在将 `TableProgressEvent` 写入文件日志（JSONL）前，必须按 `JobId + Table + Phase` 进行 coalesce：对于相同 key，只有当距离上次写入时间 >= 节流阈值时才真正落文件。
  - UI（Area B）不受该节流限制，仍然可以按 `ui.refreshIntervalMs` 高频刷新以保证界面实时性。
  - 终态写入（硬规则）：当 table 到达终态（`Completed` / `Failed` / `Skipped`）时，Host **必须立即**写入一次最终的 progress log（不受节流限制），以保证像“300ms 完成”的表也会在日志文件中留下关键终态记录。

Correlation / scope（Normative）：

- Host 在构建 job-scope 时必须把 `JobId` 注入到 Serilog 的上下文（例如 `LogContext.PushProperty("JobId", jobId)` 或 `Log.ForContext("JobId", jobId)`），使得所有后续写入的日志（控制台/文件）都携带该 property 以便关联查询。

- Core 继续通过 `ILogService` 写日志；Host 负责配置 Serilog sinks。
- Area C 的“最近 N 行日志”由 Console Host 自己维护一个**有界 ring buffer**：
  - 在 Serilog pipeline 中增加一个 Host 自定义的 in-memory sink（bounded queue）。
  - 该 sink 仅保存最近 N 条日志（建议保存：timestamp、level、rendered message、以及可选的结构化属性）。
  - UI 渲染从 ring buffer 读取并展示最近 N 行。

文件路径规则：

- `logging.file.directory` 若为相对路径，则相对进程工作目录。
- 文件名规则（硬规则）：`<fileNamePrefix>-<timestamp>.jsonl`
  - `timestamp` 推荐使用 UTC：`yyyyMMdd-HHmmss`
  - 示例：`logs/reportsyncer-20260102-231530.jsonl`
- 清理规则（硬规则）：
  - 每次进程启动时，若目录中匹配 `<fileNamePrefix>-*.jsonl` 的文件数量超过 `logging.file.retentionCount`（默认 20），则按时间从旧到新删除多余文件。
  - 删除失败不得影响本次运行（记录 warning 即可）。

---

## 6. 端到端执行流程（Host 编排）

### 6.1 运行算法（High-level）

1. 解析 CLI（仅 `--config` / `--dry-run`）。
   - 参数非法/未知 → 打印 usage → 退出码 **1**。
2. 加载 `appsettings.json`（可缺省）并应用默认值。
3. 计算有效运行参数（按 3.2 优先级规则）：
   - `effectiveJobConfigPath`
   - `dryRunFlag`（用于构造 `RuntimeOverrides.DryRun`；最终是否 dry-run 以 Core 合并结果为准）
   - 若 `effectiveJobConfigPath` 为空 → usage error → 退出码 **1**。
4. 初始化日志（Serilog sinks：Console + JSON File + ring buffer）。
5. 构建 root DI 容器并解析 Host 适配器 + Core 的“与 job 无关”服务（见第 4 节）。
6. 通过 `IConfigurationProvider.LoadAndValidateAsync(effectiveJobConfigPath, overrides, ct)` 加载/校验 YAML（overrides 由 Host 构造，例如 `RuntimeOverridesFactory.FromCliOptions(dryRunFlag: ...)`），得到 `effectiveConfig`。
   - `ConfigurationException` → 退出码 **2**。
7. 以 YAML 顺序枚举 jobs（若需要选择 job，仅允许交互式 prompt 或 Host 配置策略；不在 CLI 扩展）。
8. 对每个 job：
   0. 由 `effectiveConfig + job` 构造 job-scope DB 工厂（见 4.3）：source/target 的 `IDbConnectionFactory`、以及按 name 的 `Func<string, IDbContext>`。
   1. 运行 pre-flight（获得 `PreFlightResult`；执行顺序来自 `PreFlightResult.Schema.ExecutionPlan`）。
      - Console **展示** plan（不得计算/重排）。
   2. 若 `execution.confirmEachJob == true` 且当前为交互式：
      - 询问是否继续执行该 job；用户拒绝则记录日志并跳过（不视为失败）。
   3. 创建 job-scope 的 `ISyncOrchestrator` 实例（绑定当前 job 的 `ITableRunner`/连接工厂），然后调用 `RunJobAsync(effectiveJobConfigPath, jobName, overrides, ct)` 执行（dry-run 必须通过 `RuntimeOverrides.DryRun` 或 YAML 的 `run.dryRun` 进入 Core）。
9. 汇总结果并返回退出码（见第 8 节）。

### 6.2 多 job 的退出码规则

规则：

- 运行多个 job 时，最终退出码取本次进程内观察到的**最严重**退出码：
  - `0 < 2 < 3 < 4 < 5 < 6 < 1`
- 若用户 Ctrl+C 取消，则最终退出码为 **6**（即使之前某些 job 成功）。

---

## 7. Console UI（仅展示层）

### 7.1 屏幕布局（概念）

> 说明：本节描述“屏幕区域切分 + 每个区域应展示的最小信息”。
> 具体渲染实现可自由（全屏重画/分区重画），但不得偏离各区域数据来源与 7.3 的展示规则。

运行中布局（推荐）：

```
+-----------------------------------------------------------------------+
| [Area A: Header & Context]                                            |
| ReportSyncer | Config: <path> | Dry-Run: <ON/OFF> | Job: <name> <i>/<n>|
+-----------------------------------------------------------------------+
| [Area B: Table Grid + Summary + Controls]                             |
| Phase: <PreFlight/Delete/Insert> | Filter: <a|f|r> | Page: <p>/<P>     |
| Summary: Total <t> | Running <r> | Completed <c> | Failed <f> | Skipped <s> |
| --------------------------------------------------------------------- |
| TABLE (schema.table)        PHASE      PROGRESS    STATUS    REMARKS  |
| dbo.Orders                  Insert     [====>   ]  Running   ETA: 12s |
| dbo.Customers               Pending    [        ]  Pending   Waiting  |
| dbo.Payments                Insert     [===>    ]  Failed    <err...> |
+-----------------------------------------------------------------------+
| [Area C: Rolling Logs (last N lines)]                                 |
| [10:05:01 INF] Schema inspection for dbo.Orders completed.            |
| [10:05:02 WRN] Large delete detected (85%).                           |
| [10:05:03 INF] Start inserting into dbo.Orders...                     |
+-----------------------------------------------------------------------+
| [Area D: Footer / Hints]                                              |
| Ctrl+C: cancel | n/]/p/[: page | a/f/r: filter | Logs: <logPath>       |
+-----------------------------------------------------------------------+
```

运行结束布局（非交互也必须可判读）：

```
+-----------------------------------------------------------------------+
| ReportSyncer — Summary                                                |
| Config: <path> | Job: <name> <i>/<n> | Dry-Run: <ON/OFF>               |
| ExitCode: <0|2|3|4|5|6|1> | Duration: <hh:mm:ss>                       |
| --------------------------------------------------------------------- |
| Tables: Total <t> | Completed <c> | Skipped <s> | Failed <f>            |
| Failed Top N:                                                          |
|  - dbo.Payments: <short reason>                                        |
|  - dbo.Orders: <short reason>                                          |
| Logs: <logPath>                                                        |
+-----------------------------------------------------------------------+
```

> 备注：
> - Area B 的“Failed 置顶”是展示规则的一部分（见 7.3.1），因此示例中 Failed 行可能出现在列表最前。
> - 运行结束页的 Summary 字段要求见 7.5；即使 UI 不可用（stdin 重定向）也必须输出 Summary。

### 7.2 各区域数据来源（必须遵守）

- **Area A**
  - 有效输入：`effectiveJobConfigPath`、dry-run（显示为“override/最终值”均可；最终值以 Core 合并结果为准，例如 `PreFlightResult.DryRun`）
  - 当前 job 名称与序号
- **Area B**
  - **数据集合**来自 `PreFlightResult.Schema.ExecutionPlan`（这是“有哪些表要做”的唯一来源）
  - **执行顺序（事实）**来自 `PreFlightResult.Schema.ExecutionPlan`（Console 不得改变执行顺序）
  - **运行态（展示）**来自 Host 聚合的事件流：
    - job 级：`JobProgressEvent`（当前 job phase / 总体信息）
    - table 级：`TableProgressEvent`（每表 phase/kind/rows/eta 等）
- **Area C**
  - UI 日志来自 Host 的 Serilog ring buffer（滚动窗口保留最近 N 行）
  - N 由 `appsettings.json: ui.areaC.maxLines` 控制（默认 `200`；非法值回退 `200`）
  - Host 自己的日志（启动、配置加载等）也会进入 ring buffer；无需 Core 事件支持
- **Area D**
  - 提示信息、日志文件路径、取消提示（Ctrl+C）

### 7.3 Area B 交互规则（分页/过滤/排序；仅影响展示）

展示规则（硬规则）：

- Area B 一次只展示第 `pageIndex` 页（从 0 开始计数），每页 `ui.areaB.pageSize` 行。
  - `ui.areaB.pageSize` 默认值：`20`。
  - `ui.areaB.pageSize` 必须为正整数；非法值回退为 `20`。

- 刷新频率（硬规则）：
  - UI 渲染循环每 `ui.refreshIntervalMs` 刷新一次；默认 `250ms`。
  - 为避免闪烁/CPU 过载：`ui.refreshIntervalMs` 不得小于 `100ms`；小于该值时按 `100ms` 处理。

- 计数汇总（硬规则）：
  - Area B 顶部必须显示计数汇总：`Total / Running / Completed / Failed / Skipped`。

#### 7.3.1 排序规则（仅展示排序，不影响执行）

排序优先级（硬规则，写死）：

1. **失败可见性优先（Pinned）**：只要存在 `Failed` 表，所有 `Failed` 表必须始终置顶显示（不受过滤以外的排序影响）。
2. 主排序键（Status/Phase）：`Running` > `Failed` > `Pending` > `Completed`。
   - `Running`：`Kind` 为 `Started`/`InProgress`
   - `Failed`：`Kind` 为 `Failed`
   - `Pending`：尚未收到该表的任何 `TableProgressEvent`
   - `Completed`：`Kind` 为 `Completed`/`Skipped`
3. 次排序键（TableName）：按 `schema.table` 的字母序（ordinal，不进行本地化排序）。
4. 稳定性规则：当 `TableName` 相同或缺失时，按 `ExecutionPlan.InsertOrder` 升序作为最后的稳定排序键。

> 说明：上述排序仅影响“展示顺序”，不得影响 Core 的执行顺序。

#### 7.3.2 过滤规则（仅展示过滤，不影响执行）

- `a`：显示全部（默认）
- `f`：仅显示 Failed
- `r`：仅显示 Running + Pending

过滤语义（硬规则）：

- 过滤为 **substring** 匹配（非 regex）。
- 默认 **大小写不敏感**（case-insensitive，使用 ordinal ignore-case）。
- 过滤目标字段：`schema.table`（与 Area B 表名展示一致）。

#### 7.3.3 翻页规则（仅展示翻页）

- `n` 或 `]`：下一页
- `p` 或 `[`：上一页
- 非交互式场景（stdin 重定向）下：禁用键盘控制，固定为 `a` + 第 1 页。

可配置项边界（Normative）：

- Area B 允许从 `appsettings.json` 配置：
  - `ui.areaB.pageSize`
  - `ui.refreshIntervalMs`
- 排序优先级、失败置顶规则、过滤语义（substring + ignore-case）为 **硬规则**，不得通过配置改变（避免出现多版本行为）。

### 7.4 UI 刷新与线程模型（硬规则，必须遵守）

目标：避免实现者在 `IJobProgressReporter.Report(...)` 里直接渲染 UI 或直接写 Console，导致闪屏、丢事件、死锁或线程安全问题。

硬规则（MUST）：

- `IJobProgressReporter.Report(JobProgressEvent/TableProgressEvent)` **不得**直接渲染 UI、不得调用 `Console.WriteLine`、不得做任何“清屏/重画”行为；只能把事件写入一个线程安全的队列（例如 `Channel<T>` 或 `ConcurrentQueue<T>`）。
- `Report(...)` 必须是：
  - **non-blocking / 低开销**：不得等待 UI、不得做 I/O、不得持有长时间锁；
  - **不抛异常**：即使事件字段缺失/无效也必须降级处理（记录 warning 并忽略该事件），不得让同步任务链路被 UI 逻辑搞挂。
- UI 渲染必须集中在一个单独的 UI loop：
  - 每 `ui.refreshIntervalMs`（或更快，取决于实现）执行一次：drain 队列 → 更新内存状态 → 重画屏幕；
  - 重画屏幕必须是“全屏/分区重画”的一致策略（同一 tick 内完成），不得在事件到达时零散输出导致画面抖动。
- 键盘输入（翻页/过滤等）必须与 UI loop 协作：
  - 输入循环只更新 UI 状态（`pageIndex/filter`），不得直接输出；
  - 不得阻塞事件消费（输入读取不得卡住 drain/render）。

说明（实现自由度）：

- 队列与 UI loop 的具体实现（`Channel`/`ConcurrentQueue`、是否额外一个 background task）由 Host 决定，但上述行为约束是硬规则。

### 7.5 运行结束 Summary（硬规则；尤其非交互模式）

目标：内部工具的最终价值是“可判读”，不只是“看着热闹”。当 UI 不可用（非交互模式、日志重定向）时，也能凭 Summary 快速判断结果。

硬规则（MUST）：

- 进程结束前必须输出一段 Summary（至少写到 Console；并进入日志文件）。
- Summary 必须包含：
  - 总表数：`Total`
  - 结局计数：`Completed` / `Skipped` / `Failed`（必要时可增加 `Pending`）
  - 总耗时：`Duration`
  - 失败表 Top N（建议 N=10）：`schema.table` + 简短原因摘要（来自 `TableProgressEvent.ErrorMessage`、`TableResult.ErrorMessage` 或异常摘要）
  - 本次运行日志文件路径（`logs/<prefix>-<timestamp>.jsonl`）
- 非交互模式（stdin 重定向）下：必须输出 Summary（不得省略/只靠 UI footer）。

---

## 8. 退出码（以“当前代码库可执行”为准，并标注 reserved）

### 8.1 Process Exit Codes（Normative）

> 目标：退出码必须稳定、可记忆、可用于脚本/CI 判定。
> 本节为 Console Host 对外契约；Host 只做“结果/异常 → 退出码”的翻译，不泄漏内部异常细节到契约中。

|Exit code|名称|触发条件（硬规则）|说明|
|---:|---|---|---|
|0|Success|Job 成功完成；或 dry-run 成功；或“无可执行表（全 skipped）但无错误”|成功就是成功。|
|2|InvalidArguments|CLI 参数解析失败；或显示 usage/help 后用户输入仍不合法|常见约定：2 表示用法错误。|
|3|InvalidConfiguration|YAML/配置加载失败、I/O 失败、校验失败（缺字段、类型错、路径错）|配置问题要和运行时失败区分。|
|4|PreflightFailed|预检失败（例如连接失败、schema/table 不存在、权限不足等），且未进入执行阶段|运行前就该失败，不得开始任何 DML。|
|5|ExecutionFailed|进入执行后，任意 table/job 失败，最终 `JobStatus == Failed`（或等价结果）|核心失败码。|
|6|Cancelled|Ctrl+C / `CancellationToken` 导致取消，最终 `JobStatus == Cancelled`（或抛 `OperationCanceledException`）|取消是“中断”，不是“失败”。|
|1|UnhandledFatal|未捕获异常、Host 自身 bug、或无法归类的致命错误|兜底码：你不想看到，但必须有。|

### 8.2 映射规则（当前代码库 → 退出码）

> 说明：Host 需要同时处理两类信号：
> 1) Core 抛出的域异常/基础异常；
> 2) Host 自身的 CLI/运行时结果（如 job status）。

- CLI 解析/usage error：`2`
  - 例如 unknown args、缺少 `--config` 且 `appsettings.json` 也未提供默认值。
- 配置加载/校验失败：`3`
  - `ConfigurationException` / `IOException` / `FileNotFoundException` 等。
- 预检失败：`4`
  - `SchemaMismatchException` 视作 preflight 失败的一种（属于“运行前就该失败”的范畴）。
  - 其它 preflight gate 类异常（例如 `PreflightGateException`）若发生在执行前，也归入 `4`。
- 执行失败：`5`
  - `SyncExecutionException`，或其它发生在执行阶段并导致 job failed 的异常。
- 取消：`6`
  - `OperationCanceledException`。
- 未分类/未捕获：`1`
  - 其它异常统一兜底。

### 8.3 多 job 的退出码合并规则

- 运行多个 job 时，最终退出码取本次进程内观察到的**最严重**退出码。
- 严重性顺序（从轻到重）：
  - `0 < 2 < 3 < 4 < 5 < 6 < 1`
- 取消优先：若用户 Ctrl+C 取消，则最终退出码为 `6`（即使之前某些 job 成功/失败）。
- fatal 优先：若出现 `UnhandledFatal (1)`，则最终退出码为 `1`。

---

## 9. 不变式（dry-run / preflight / cancellation）

这些规则违反即为 bug：

### 9.1 Pre-flight gate（门禁）

- Pre-flight 失败时，**不得开始**任何 delete/insert（目标库不得发生 DML）。

### 9.2 Dry-run purity（dry-run 纯净性）

- 当有效 dry-run 为 `true` 时，目标库不得执行任何 DML：
  - `INSERT/DELETE/UPDATE/MERGE/TRUNCATE`
- 允许只读操作（schema/permission probes、`SELECT COUNT(*)` 等），且应明确标识为只读。

### 9.3 Ctrl+C 取消语义

- Ctrl+C 必须映射为 `CancellationToken` 并传递到所有 Core 调用链路。
- 一旦观察到取消，后续不得启动新的 batch/DML。
- 进程退出码为 **6**。

