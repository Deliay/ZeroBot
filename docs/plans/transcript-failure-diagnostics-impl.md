# 实现方案：转写任务第三方失败可观测性（recording-transcript-consumer）

- 日期：2026-09-18
- 状态：待实施
- 分支：`feat/transcript-failure-diagnostics`（基于 origin/main，commit 257fd43）
- 关联文档：[transcript-failure-diagnostics-prd.md](transcript-failure-diagnostics-prd.md)（产品需求）、[transcript-failure-diagnostics-tech.md](transcript-failure-diagnostics-tech.md)（技术提案）

## 范围

代码改动全部位于 vtuber server 侧仓库 `src/servers/recording-transcript-consumer/`（**不在本仓库**），本仓库仅提交本文档。共改 2 个文件，无配置变更、无存储迁移、不改转写流程与重试策略：

| # | 文件 | 动作 |
|---|------|------|
| 1 | `src/servers/recording-transcript-consumer/Asr/AliFunAsrService.cs` | 分阶段结构化日志、`FunAsrException`/`FunAsrErrorKind`、异常原样穿透、上下文贯穿、空结果告警 |
| 2 | `src/servers/recording-transcript-consumer/Tasks/TranscriptTaskRunner.cs` | 失败日志原样记录异常（约 line 112） |

## 实施步骤

### 步骤 0：确认目标代码现状（前置）

技术提案基于生产日志堆栈推断，实施前先在 vtuber server 仓库核对以下事实，如有出入按实际情况微调（不改变方案方向）：

1. `AliFunAsrService.TranscribeAsync` 的 channel 流水线结构，确认 upload / submit / poll / fetch 四个阶段的实际代码边界；
2. 现有错误分类枚举（`CLIENT_ERROR` 等）的定义位置与所有使用点；
3. line 113（channel 消费端）与 line 143（外层）异常包装的确切位置；
4. `TranscribeAsync` 当前可获得的上下文：转写任务 ID、recording 标识、音频字节数、时长（没有时长则以字节数兜底，不新增时长探测）；
5. `TranscriptTaskRunner.RunAsync` 失败出口（约 line 112）是否吞异常类型或重建消息。

### 步骤 1：新增 `FunAsrException` 与错误分类（R3）

文件：`Asr/AliFunAsrService.cs`（同文件内新增）

1. 新增枚举与异常类型：

```csharp
public enum FunAsrErrorKind { NetworkTimeout, ClientError, ServerError, TaskFailed, Unknown }

public sealed class FunAsrException(string stage, FunAsrErrorKind kind, string message,
        string? taskId = null, int? httpStatus = null, string? responseSnippet = null,
        Exception? inner = null)
    : Exception($"FunASR task failed ({kind}) at {stage}: {message}"
        + (httpStatus is null ? "" : $" [http={httpStatus}]")
        + (responseSnippet is null ? "" : $" [response={responseSnippet}]"), inner)
{
    public string Stage => stage;
    public FunAsrErrorKind Kind => kind;
    public string? TaskId => taskId;
}
```

2. 新增响应 body 截断辅助（512 字符上限）：

```csharp
private static string Truncate(string body) =>
    body.Length > 512 ? body[..512] + "…(truncated)" : body;
```

要点：消息首段保持 `FunASR task failed ({kind})` 格式，与现有日志检索习惯兼容；分类枚举值序列化到消息时用 `PascalCase`，与 PRD 分类标签（`NETWORK_TIMEOUT` 等）为同一语义的代码表达，排查时一一对应。

### 步骤 2：捕获点落实分类规则（R3）

文件：`Asr/AliFunAsrService.cs`

在各阶段调用 FunASR 的位置按技术提案 §2 表格捕获并分类：

| 捕获到 | 分类 | 附带信息 |
|---|---|---|
| `HttpRequestTimeoutException`、`TaskCanceledException`/`OperationCanceledException`（外部 ct 未取消时）、socket 超时 | `NetworkTimeout` | 阶段耗时 |
| HTTP 4xx | `ClientError` | status + body 摘要（截断） |
| HTTP 5xx | `ServerError` | status + body 摘要（截断） |
| 任务状态 `failed` | `TaskFailed` | FunASR 错误码 + message 原文 |
| 其他异常 | `Unknown` | 保留 inner exception |

关键约束：

- 外部 `CancellationToken` 触发的取消**不分类、不告警**，直接抛 `OperationCanceledException`；
- channel 内抛出的 `FunAsrException` 在消费端（现 line 113 附近）**原样穿透**：catch 后 `throw;`，跨线程/跨 await 边界时用 `ExceptionDispatchInfo.Throw`；禁止再包 `InvalidOperationException`；
- 替换现有 `CLIENT_ERROR` 笼统标签的全部产出点。

### 步骤 3：分阶段结构化日志（R1）

文件：`Asr/AliFunAsrService.cs`

1. 定义阶段名常量：

```csharp
private static class Stages
{
    public const string Upload = "upload";
    public const string Submit = "submit";
    public const string Poll   = "poll";
    public const string Fetch  = "fetch";
}
```

2. `TranscribeAsync` 入口启动总 `Stopwatch`，每个阶段各启一个 `Stopwatch`：
   - 进入：`LogDebug("FunASR {Stage} begin: taskId={TaskId} recording={RecordingId}", ...)`；
   - 完成：`LogInformation("FunASR {Stage} done: taskId={TaskId} elapsed={ElapsedMs}ms ...", ...)`；
   - 失败：抛 `FunAsrException`（步骤 2），由统一出口打 Warning（步骤 4）。
3. poll 阶段特殊处理：每轮轮询的状态变化只打 `Debug`，`Information` 仅记最终一次完成/失败，避免刷屏。
4. 全部日志使用 `ILogger` message template 结构化参数，禁止字符串拼接。

### 步骤 4：上下文贯穿与出口三分类（R2、R4）

文件：`Asr/AliFunAsrService.cs`

1. 上下文贯穿：转写任务 ID、recording 标识、音频字节数（时长若现成可得则带上，否则不新增探测）从入口贯穿到所有阶段日志与异常。
2. `TranscribeAsync` 出口区分三种结果：
   - 成功且 `segments > 0`：`LogInformation("FunASR transcribe finished: taskId={TaskId} segments={Segments} elapsed={ElapsedMs}ms")`；
   - 成功但 `segments == 0`：`LogWarning("FunASR transcribe returned EMPTY: taskId={TaskId} recording={RecordingId} audioBytes={AudioBytes} elapsed={ElapsedMs}ms")`；
   - 失败：`LogWarning(ex, "FunASR transcribe failed: taskId={TaskId} recording={RecordingId} audioBytes={AudioBytes}")`（`ex` 为 `FunAsrException`，已携带 stage/kind/http/response）。

### 步骤 5：`TranscriptTaskRunner` 失败出口（R2）

文件：`Tasks/TranscriptTaskRunner.cs`（约 line 112）

1. 保持 `LogWarning(ex, "Transcript task {TaskId} failed")` 形式，`ex` 原样传入，不重复打印第三方细节（`FunAsrException` 已携带）；
2. 若当前实现吞异常类型或重建消息，改为原样记录 `ex`；
3. 非 FunASR 失败（IO、数据库等）路径不动。

## 验证

1. 编译：vtuber server 仓库 `dotnet build` 通过；
2. 单测（若该项目有测试工程，按现有模式补充；无则跳到 3）：
   - 模拟 FunASR 返回 4xx / 5xx / 任务 failed / 超时，断言 `FunAsrException.Kind` 分别为 `ClientError` / `ServerError` / `TaskFailed` / `NetworkTimeout`，消息含 status 或 FunASR 错误码；
   - 模拟成功但 `segments == 0`，断言产生 `Warning` 级日志（`ILogger` 测试替身）；
   - 模拟外部 ct 取消，断言抛 `OperationCanceledException` 且无 Warning 告警；
3. 实跑验证：本地/测试环境指向不可达端点触发超时，人工核对日志含阶段名、耗时、`NetworkTimeout` 分类，不再出现笼统 `CLIENT_ERROR`；
4. 对照 PRD 验收标准 1–6 逐条核对（重点：#2 超时分类正确、#6 正常路径日志量不明显增加）。

## 非目标

- 不改变转写流程与重试策略；
- 不接入 metrics / tracing / 告警平台；
- 不更换 ASR 供应商，不修改 FunASR 部署；
- 不处理「空转写」的业务补偿（只要求可观测）；
- 不新增音频时长探测（无现成元数据时以字节数兜底）。

## 风险与注意

- 目标代码不在本仓库，实施前必须完成步骤 0 的现状核对，阶段边界以实际代码为准；
- 响应 body 一律走 `Truncate`，防止超大响应刷爆日志；
- 成功路径（含 poll 每轮）不得产生 `Warning`，避免噪音淹没真实问题；
- 异常穿透改动只涉及 channel 消费端的包装点，不改变 channel 流水线本身的并发结构。
