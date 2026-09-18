# 技术提案：转写任务第三方失败可观测性（recording-transcript-consumer）

- 日期：2026-09-18
- 状态：待评审
- 关联文档：[transcript-failure-diagnostics-prd.md](transcript-failure-diagnostics-prd.md)（产品需求）

## 现状分析

> 目标代码不在本仓库，位于 vtuber server 侧 `src/servers/recording-transcript-consumer/`（`.NET`，命名空间 `Z.Vtuber.RecordingTranscript.Consumer`）。以下分析基于上游失败日志的堆栈与消息格式。

失败现场（2026-09，生产日志）：

```
warn: AliFunAsrService[0]
      FunASR task-failed: taskId=78939e1639754412b50d570a212b943c error=FunASR task failed (CLIENT_ERROR): request timeout after 23 seconds.
info: AliFunAsrService[0]
      FunASR transcribe finished: taskId=... segments=0
warn: TranscriptTaskRunner[0]
      Transcript task 6aaace4ca868b617d15613d7 failed
      System.InvalidOperationException: FunASR task failed (CLIENT_ERROR): request timeout after 23 seconds.
         at System.Threading.Channels.ChannelReader`1.ReadAllAsync(...)   ← AliFunAsrService.cs:113
         at AliFunAsrService.TranscribeAsync(...)                          ← AliFunAsrService.cs:143
         at TranscriptTaskRunner.RunAsync(...)                             ← TranscriptTaskRunner.cs:112
```

从堆栈与消息可推断的现状：

1. `AliFunAsrService.TranscribeAsync` 内部以 `Channel` 串联 FunASR 交互（很可能：上传 → 提交任务 → 轮询任务状态 → 产出 segment），line 113 处于 channel 消费端，line 143 为外层包装。异常在 channel 内抛出后被 `InvalidOperationException` 二次包装，**原始异常类型与来源阶段已丢失**。
2. 错误消息格式 `FunASR task failed (CLIENT_ERROR): {message}` 说明存在一个分类枚举，但「request timeout after 23 seconds」被归为 `CLIENT_ERROR`，**分类明显错误**（超时与 4xx 混为一谈），且未保留 HTTP 状态码、FunASR 错误码等原始信息。
3. `task-failed` 与 `transcribe finished: segments=0` 两条日志**没有任何音频上下文**（哪条 recording、音频多长多大），多个任务并发时只能靠 taskId 人肉关联。
4. 失败路径与成功路径共用 `transcribe finished` 出口，`segments=0` 的空结果没有单独告警，「静默空转写」无法被发现。

## 实现方案

改动集中在 `recording-transcript-consumer` 的两个文件，不动转写流程与重试逻辑。

### 1. `Asr/AliFunAsrService.cs` — 分阶段结构化日志

在 `TranscribeAsync` 内显式划分阶段（与现有 channel 流水线对齐），约定阶段名常量：

```csharp
private static class Stages
{
    public const string Upload = "upload";        // 上传音频
    public const string Submit = "submit";        // 提交转写任务
    public const string Poll   = "poll";          // 轮询任务状态
    public const string Fetch  = "fetch";         // 拉取转写结果
}
```

每个阶段：

- 进入：`LogDebug("FunASR {Stage} begin: taskId={TaskId} recording={RecordingId}", ...)`。
- 完成：`LogInformation("FunASR {Stage} done: taskId={TaskId} elapsed={ElapsedMs}ms ...", ...)`（poll 阶段只打最终一次，不打每轮轮询，避免刷屏；轮询中的状态变化打 Debug）。
- 失败：抛出见 §2 的 `FunAsrException`，由统一出口打 `LogWarning`。

用 `Stopwatch` 记录各阶段耗时与总耗时。日志全部使用结构化模板参数（`ILogger` message template），禁止字符串拼接，便于后续按字段检索。

### 2. `Asr/AliFunAsrService.cs` — 异常类型与错误分类

新增 `FunAsrException : Exception`（可放同文件）：

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

分类规则（在捕获点落实）：

| 捕获到 | 分类 |
|---|---|
| `TaskCanceledException`/`OperationCanceledException` 且非外部 ct 取消、`HttpRequestTimeoutException`、socket 超时 | `NetworkTimeout` |
| HTTP 4xx | `ClientError`（附 status + body 摘要） |
| HTTP 5xx | `ServerError`（附 status + body 摘要） |
| 任务状态 `failed`（FunASR 业务错误码/message） | `TaskFailed`（附 FunASR code + message 原文） |
| 其他 | `Unknown`（保留 inner exception） |

关键点：

- **不再用 `InvalidOperationException` 二次包装**，channel 内抛出的 `FunAsrException` 原样穿透（消费端 catch 后 `throw;` 或用 `ExceptionDispatchInfo`），堆栈保留原始 throw 点。
- 响应 body 摘要统一截断：`body.Length > 512 ? body[..512] + "…(truncated)" : body`。
- 外部 `CancellationToken` 触发的取消**不算失败**，直接抛 `OperationCanceledException`，不进错误分类。

### 3. `Asr/AliFunAsrService.cs` — 上下文贯穿与结果校验

- `TranscribeAsync` 入口接受（或从现有参数推导）并贯穿记录：转写任务 ID、recording 标识、音频字节数、音频时长（若当前没有时长元数据，记字节数兜底；不强求本次新增时长探测）。
- 出口处区分三种结果：
  - 成功且 `segments > 0`：`LogInformation("FunASR transcribe finished: taskId={TaskId} segments={Segments} elapsed={ElapsedMs}ms")`。
  - 成功但 `segments == 0`：`LogWarning("FunASR transcribe returned EMPTY: taskId={TaskId} recording={RecordingId} audioBytes={AudioBytes} elapsed={ElapsedMs}ms")`。
  - 失败：`LogWarning(ex, "FunASR transcribe failed: taskId={TaskId} recording={RecordingId} audioBytes={AudioBytes}")`，异常对象本身已携带 stage/kind/response。

### 4. `Tasks/TranscriptTaskRunner.cs` — 失败出口补充

`RunAsync` 的失败日志（现 line 112 附近）：

- 保持 `LogWarning(ex, "Transcript task {TaskId} failed")`，因 `FunAsrException` 自带完整上下文，无需重复打印第三方细节。
- 若当前 catch 会吞异常类型或重建消息，改为原样记录 `ex`。
- 非 FunASR 失败（IO、数据库等）维持现状。

### 5. 验证

- 单测（如该项目有测试工程，按现有模式补充）：
  - 模拟 FunASR 返回 4xx / 5xx / 任务 failed / 超时，断言异常 `Kind` 分别为 `ClientError`/`ServerError`/`TaskFailed`/`NetworkTimeout`，且消息含 status 或 FunASR 错误码。
  - 模拟成功但空结果，断言产生 `Warning` 级日志（可用 `ILogger` 测试替身）。
- 本地或测试环境跑一次真实失败（指向不可达端点触发超时），人工核对日志含阶段、耗时、分类。
- `dotnet build` 通过。

## 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/servers/recording-transcript-consumer/Asr/AliFunAsrService.cs` | 改：分阶段日志、`FunAsrException`/`FunAsrErrorKind`、上下文贯穿、空结果告警 |
| `src/servers/recording-transcript-consumer/Tasks/TranscriptTaskRunner.cs` | 改：失败日志原样记录异常（约 line 112） |
| `docs/plans/transcript-failure-diagnostics-prd.md` | 新增（产品需求，本仓库） |
| `docs/plans/transcript-failure-diagnostics-tech.md` | 新增（本文档，本仓库） |

## 关键决策点

1. **只加日志，不改流程**：本次目标是可观测性，重试/补偿策略不变，把行为变更风险降到零。
2. **自定义异常携带分类与阶段**：替代现有的 `CLIENT_ERROR` 笼统标签；分类在捕获点按 §2 表格落实，杜绝「超时算客户端错误」。
3. **异常不再二次包装为 `InvalidOperationException`**：保留原始堆栈与类型，channel 消费端用 `ExceptionDispatchInfo` 或 `throw;` 原样穿透。
4. **响应 body 截断 512 字符**：兼顾排查信息量与日志体积。
5. **轮询过程只打 Debug**：poll 是高频循环，Information 级只记最终态，避免正常路径日志膨胀。
6. **外部取消与失败分离**：`CancellationToken` 取消不进错误分类，避免关停时产生误导性告警。
