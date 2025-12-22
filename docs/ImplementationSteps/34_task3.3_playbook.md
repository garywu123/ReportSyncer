

# Task 3.3 Playbook: Schema Mapping + Context Column Injection

## 1) Minimal Contract（只给形状，不给完整类）

> 目标：让 Executor AI 知道数据长什么样、要实现哪个方法、返回什么。

### 1.1 Enums（名称 + 成员）

* `MappingKind`

  * `OneToOne`
  * `Constant`
  * `ContextColumn`
  * `Ignored`

* `SchemaMappingErrorCode`

  * `SourceTableNotFound`
  * `TargetTableNotFound`
  * `TargetColumnMissingInSource`
  * `TargetColumnTypeIncompatible`
  * `ContextInjectionParameterMissing`
  * `ContextInjectionAmbiguous`
  * `ContextColumnMissingInTarget`
  * `PrimaryKeyMissing`

### 1.2 Records（名称 + 关键属性）

* `SchemaMappingError`

  * `Code: SchemaMappingErrorCode`
  * `SourceTable: TableIdentifier?`
  * `TargetTable: TableIdentifier?`
  * `ColumnName: string?`
  * `Message: string`
  * `SourceType: string?`
  * `TargetType: string?`

* `ColumnMapping`

  * `SourceColumn: ColumnSchema?`
  * `TargetColumn: ColumnSchema`
  * `Kind: MappingKind`
  * `ConstantValue: object?`

* `TableMapping`

  * `SourceTable: TableIdentifier`
  * `TargetTable: TableIdentifier`
  * `ColumnMappings: IReadOnlyList<ColumnMapping>`
  * `HasWarnings: bool`

* `SchemaMappingResult`

  * `Success: bool`
  * `Errors: IReadOnlyList<SchemaMappingError>`
  * `TableMappings: IReadOnlyDictionary<TableIdentifier, TableMapping>`

### 1.3 Helper Methods（只列签名，必须存在）

* `SchemaSnapshot.TryGetTable(TableIdentifier id, out TableSchema table) -> bool`
* `TableSchema.TryGetColumn(string name, out ColumnSchema column) -> bool`

  * **列名比较必须 `OrdinalIgnoreCase`**

### 1.4 Interface（只允许一个方法签名）

* `ISchemaMapper`

  * `MapJob(SchemaSnapshot sourceSnapshot, SchemaSnapshot targetSnapshot, SyncJobConfig job, ConnectionConfig sourceConnection, ConnectionConfig targetConnection, SchemaPolicyConfig schemaPolicy) -> Result<SchemaMappingResult>`

> `Result<T>`：必须复用 repo 现有模式（DotNetToolkit / Core）。不要自创一个新 Result。

---

## 2) Atomic Implementation Steps（线性、无脑执行）

### Step 1: Scaffolding（创建文件与最小骨架）

1. 在 `ReportSyncer.Core/Schema/Mapping/` 创建文件：

   * `MappingKind.cs`
   * `SchemaMappingErrorCode.cs`
   * `SchemaMappingError.cs`
   * `ColumnMapping.cs`
   * `TableMapping.cs`
   * `SchemaMappingResult.cs`
   * `ISchemaMapper.cs`
   * `SchemaMapper.cs`
2. 每个文件必须统一 namespace：`ReportSyncer.Core.Schema.Mapping`
3. `SchemaMapper.cs` 需要 `using`：

   * `System`
   * `System.Collections.Generic`
   * `System.Linq`
   * `ReportSyncer.Core.Configuration`
   * `ReportSyncer.Core.Schema`（按你项目实际 namespace）
4. 在 `SchemaSnapshot` 与 `TableSchema` 加 helper：

   * 如果已经有 `TryGetTable/TryGetColumn`：**不要重复造**
   * 如果没有：新增这两个签名（内部用字典或 LINQ，但必须 `OrdinalIgnoreCase`）

---

### Step 2: Core Algorithm（Pseudo-code，用固定变量名 + If-Then-Else）

> **你必须按这个变量名写**，否则后面的测试矩阵对不上。

#### Step 2.1: 初始化固定变量

1. 创建集合：

   * `List<SchemaMappingError> errors = new();`
   * `Dictionary<TableIdentifier, TableMapping> tableMappings = new();`
2. 计算注入开关：

   * `bool enableContextInjection = (sourceConnection.Type == DbType.Application) && (targetConnection.Type == DbType.Reporting);`
3. 决定 context 列名：

   * `string contextColumnName = "CustomerId";`
   * 如果 job 或 policy 里有配置覆盖（你的配置模型若存在该字段）：用覆盖值，否则保持默认
4. 准备 job 参数：

   * `bool hasContextParam = job.Parameters.ContainsKey(contextColumnName)`（key 比较忽略大小写）
   * `object? contextValue = hasContextParam ? job.Parameters[contextColumnName] : null;`

#### Step 2.2: 遍历每个 TableTask（按目标列驱动映射）

对 `foreach (var tableTask in job.Tables)` 执行以下步骤，顺序不能变：

1. 解析表 ID：

   * `TableIdentifier sourceTableId = TableIdentifier.Parse(tableTask.Source);`
   * `TableIdentifier targetTableId = TableIdentifier.Parse(tableTask.Target);`
2. 取表 schema：

   * If `!sourceSnapshot.TryGetTable(sourceTableId, out var sourceTable)`

     * Then `errors.Add(Error(SourceTableNotFound, sourceTableId, targetTableId, null, ...))`
     * Then `continue;`
   * If `!targetSnapshot.TryGetTable(targetTableId, out var targetTable)`

     * Then `errors.Add(Error(TargetTableNotFound, sourceTableId, targetTableId, null, ...))`
     * Then `continue;`
3. 建列索引（忽略大小写）：

   * `var sourceCols = sourceTable.Columns.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);`
   * `var targetCols = targetTable.Columns.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);`
4. Context 注入预检（只要 enableContextInjection=true 才做）：

   * `bool sourceHasContextCol = sourceCols.ContainsKey(contextColumnName);`
   * `bool targetHasContextCol = targetCols.ContainsKey(contextColumnName);`
   * 初始化：`bool contextReady = true;`
   * If `enableContextInjection == true` Then：

     * If `hasContextParam == false`

       * Then `errors.Add(Error(ContextInjectionParameterMissing, sourceTableId, targetTableId, contextColumnName, ...))`
       * Then `contextReady = false;`
     * If `targetHasContextCol == false`

       * Then `errors.Add(Error(ContextColumnMissingInTarget, sourceTableId, targetTableId, contextColumnName, ...))`
       * Then `contextReady = false;`
     * If `(hasContextParam == true) && (sourceHasContextCol == true)`

       * Then `errors.Add(Error(ContextInjectionAmbiguous, sourceTableId, targetTableId, contextColumnName, ...))`
       * Then `contextReady = false;`
5. 生成 ColumnMappings（必须遍历 targetTable.Columns）

   * `List<ColumnMapping> columnMappings = new();`
   * For each `ColumnSchema targetCol in targetTable.Columns`：

     * If `enableContextInjection == true` AND `contextReady == true` AND `targetCol.Name equals contextColumnName (ignore case)`

       * Then `columnMappings.Add(new ColumnMapping(null, targetCol, MappingKind.ContextColumn, contextValue));`
       * Then `continue;`
     * Else If `sourceCols.TryGetValue(targetCol.Name, out var sourceCol) == true`

       * Then If `IsTypeCompatible(sourceCol, targetCol) == false`

         * Then `errors.Add(Error(TargetColumnTypeIncompatible, sourceTableId, targetTableId, targetCol.Name, sourceCol.DbType, targetCol.DbType))`
       * Then `columnMappings.Add(new ColumnMapping(sourceCol, targetCol, MappingKind.OneToOne, null));`
       * Then `continue;`
     * Else（source 没该列）

       * If `schemaPolicy.AllowExtraTargetColumns == true` AND `(targetCol.IsNullable == true OR targetCol.IsIdentity == true)`

         * Then `columnMappings.Add(new ColumnMapping(null, targetCol, MappingKind.Ignored, null));`
       * Else

         * Then `errors.Add(Error(TargetColumnMissingInSource, sourceTableId, targetTableId, targetCol.Name, null, targetCol.DbType))`
6. 主键检查（只在 policy 要求时）

   * If `schemaPolicy.RequirePrimaryKey == true`：

     * If `targetTable.PrimaryKeyColumns.Count == 0`

       * Then `errors.Add(Error(PrimaryKeyMissing, sourceTableId, targetTableId, null, ...))`
7. 生成 TableMapping 并保存

   * `var tableMapping = new TableMapping(sourceTableId, targetTableId, columnMappings, HasWarnings: false);`
   * `tableMappings[targetTableId] = tableMapping;`

#### Step 2.3: 收尾返回 Result

1. `bool success = errors.Count == 0;`
2. `var resultValue = new SchemaMappingResult(success, errors, tableMappings);`
3. If `success == true`

   * Then `return Result.Ok(resultValue);`
4. Else

   * Then `return Result.Fail(resultValue);`
     （如果你 repo 的 Result.Fail 不支持携带 value：**按 repo 现有约定改成 Fail(errors) + 保留 resultValue 在 Fail 的附加字段里**。不要自己发明新 API。）

---

### Step 3: Edge Case Handling（错误码 + message 模板，必须原样）

实现一个内部方法（或静态函数）`CreateError(...)` 用固定模板生成 `SchemaMappingError.Message`：

* `SourceTableNotFound`
  `"Source table not found in snapshot: '{SourceTable}'."`
* `TargetTableNotFound`
  `"Target table not found in snapshot: '{TargetTable}'."`
* `TargetColumnMissingInSource`
  `"Target column '{Column}' in table '{TargetTable}' has no matching source column."`
* `TargetColumnTypeIncompatible`
  `"Incompatible types for column '{Column}' in table '{TargetTable}': source='{SourceType}', target='{TargetType}'."`
* `ContextInjectionParameterMissing`
  `"Context injection is enabled but job parameter '{ContextColumn}' is missing."`
* `ContextInjectionAmbiguous`
  `"Context injection ambiguity for column '{ContextColumn}' in table '{TargetTable}': source column exists and job parameter is also provided."`
* `ContextColumnMissingInTarget`
  `"Context injection is enabled but target table '{TargetTable}' does not contain column '{ContextColumn}'."`
* `PrimaryKeyMissing`
  `"Primary key is required by policy but missing: table '{TargetTable}'."`

**禁止事项：**

* 不允许 `try-catch` 包业务错误
* 不允许 `throw` 这些 mismatch（这里必须收集错误并返回 Result.Fail）

---

### Step 4: IsTypeCompatible（必须按 family 规则，不要靠“字符串相等”）

实现 `bool IsTypeCompatible(ColumnSchema sourceCol, ColumnSchema targetCol)`，按下面顺序判断：

1. 取类型字符串（统一小写）：

   * `string s = sourceCol.DbType.ToLowerInvariant();`
   * `string t = targetCol.DbType.ToLowerInvariant();`
2. If `s == t` Then return true
3. 定义 family：

   * `stringFamily = { "varchar","nvarchar","char","nchar","text","ntext" }`
   * `intFamily = { "tinyint","smallint","int","bigint" }`
   * `decimalFamily = { "decimal","numeric","money","smallmoney" }`
   * `dateFamily = { "date","datetime","datetime2","smalldatetime","time" }`
   * `guidFamily = { "uniqueidentifier" }`
   * `boolFamily = { "bit" }`
4. If `s` 和 `t` 在同一个 family：

   * Then return true（先放宽，后续你要做“窄化”再收紧）
5. Else return false

---

## 3) Execution-Ready Unit Test Matrix（Executor 可直接翻译成代码）

> 测试文件：`Tests/ReportSyncer.Core.Tests/Schema/Mapping/SchemaMapperTests.cs`
> 测试原则：纯内存构造 `SchemaSnapshot`、`TableSchema`、`ColumnSchema`、`SyncJobConfig`。

| Test Case Name                                        | Input Scenario                                                                                                     | Logic Path to Hit                                        | Expected Result                                                                              |
| :---------------------------------------------------- | :----------------------------------------------------------------------------------------------------------------- | :------------------------------------------------------- | :------------------------------------------------------------------------------------------- |
| MapJob_PerfectMatch_Ok                                | Source/Target 同表同列同类型；enableContextInjection=false                                                                 | Step 2.2.5 命中 OneToOne 分支                                | `Result.Ok` 且 `Value.Success=true` 且 `Errors.Count=0`                                        |
| MapJob_SourceTableMissing_Fail                        | job.Tables=1；sourceSnapshot 不含该表；target 有                                                                          | Step 2.2.2 第一个 If -> continue                            | `Result.Fail`；Errors 含 `SourceTableNotFound`；TableMappings 不包含该 targetId                     |
| MapJob_TargetTableMissing_Fail                        | source 有，targetSnapshot 不含                                                                                         | Step 2.2.2 第二个 If -> continue                            | `Result.Fail`；Errors 含 `TargetTableNotFound`                                                 |
| MapJob_TargetExtraRequiredColumn_Fail                 | target 多一列 `IsNullable=false` 且 `IsIdentity=false`；source无；AllowExtraTargetColumns=false                           | Step 2.2.5 最后 Else -> MissingInSource                    | `Result.Fail`；Errors 含 `TargetColumnMissingInSource`（列名正确）                                   |
| MapJob_TargetExtraNullableColumn_Ignored_WhenPolicyOn | 同上但 target extra 列 `IsNullable=true` 且 AllowExtraTargetColumns=true                                                | Step 2.2.5 Allowed extra -> Ignored                      | `Result.Ok` 或 `Result.Fail` 取决于其他错误；且该列 mapping.Kind==Ignored                                |
| MapJob_TypeIncompatible_Fail                          | 同名列：source.DbType="nvarchar" target.DbType="int"                                                                   | Step 2.2.5 IsTypeCompatible=false                        | `Result.Fail`；Errors 含 `TargetColumnTypeIncompatible` 且 SourceType/TargetType 填充             |
| MapJob_ContextInjection_AddsContextColumn             | sourceConnection=Application；targetConnection=Reporting；target有CustomerId；source无；job.Parameters["CustomerId"]=101 | Step 2.2.4 contextReady=true；Step 2.2.5 ContextColumn 分支 | `Result.Ok`；CustomerId mapping: `Kind=ContextColumn`、`SourceColumn=null`、`ConstantValue=101` |
| MapJob_ContextParamMissing_Fail                       | 同上但 job.Parameters 不含 CustomerId                                                                                   | Step 2.2.4 ParameterMissing -> contextReady=false        | `Result.Fail`；Errors 含 `ContextInjectionParameterMissing`                                    |
| MapJob_ContextAmbiguous_Fail                          | 同上但 source 也有 CustomerId 且 job.Parameters 也提供                                                                      | Step 2.2.4 Ambiguous -> contextReady=false               | `Result.Fail`；Errors 含 `ContextInjectionAmbiguous`                                           |
| MapJob_TargetMissingContextColumn_Fail                | enableContextInjection=true 但 target 没 CustomerId                                                                  | Step 2.2.4 ContextColumnMissingInTarget                  | `Result.Fail`；Errors 含 `ContextColumnMissingInTarget`                                        |
| MapJob_RequirePrimaryKey_TargetHasNone_Fail           | schemaPolicy.RequirePrimaryKey=true；targetTable.PrimaryKeyColumns=0                                                | Step 2.2.6 PrimaryKeyMissing                             | `Result.Fail`；Errors 含 `PrimaryKeyMissing`                                                   |
