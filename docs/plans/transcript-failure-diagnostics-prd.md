# 产品需求：转写任务第三方失败可观测性（recording-transcript-consumer）

- 日期：2026-09-18
- 状态：待评审
- 关联文档：[transcript-failure-diagnostics-tech.md](transcript-failure-diagnostics-tech.md)（技术提案）

## 背景

录播转写任务（`Z.Vtuber.RecordingTranscript.Consumer`）运行失败，但现有日志无法定位原因。一次真实失败的完整日志如下：

```
warn: AliFunAsrService[0]
      FunASR task-failed: taskId=78939e1639754412b50d570a212b943c error=FunASR task failed (CLIENT_ERROR): request timeout after 23 seconds.
info: AliFunAsrService[0]
      FunASR transcribe finished: taskId=78939e1639754412b50d570a212b943c segments=0
warn: TranscriptTaskRunner[0]
      Transcript task 6aaace4ca868b617d15613d7 failed
      System.InvalidOperationException: FunASR task failed (CLIENT_ERROR): request timeout after 23 seconds.
         at ... AliFunAsrService.cs:line 113 / 143
         at ... TranscriptTaskRunner.cs:line 112
```

问题：日志里只有一句转述的「request timeout after 23 seconds」，**没有任何第三方（FunASR）侧的原始信息**——失败发生在哪个阶段（上传？提交任务？轮询状态？拉取结果？）、第三方返回了什么、音频本身多大多长，全都不知道。任务失败后只能看到结果（segments=0、任务失败），无法排查，也无法判断是该重试、该换音频还是该找服务商。

## 目标

1. FunASR 转写失败时，日志能直接回答三个问题：**失败在哪个阶段**、**第三方返回了什么**、**处理的是哪条音频**。
2. 失败日志包含足量上下文：taskId、音频元数据（来源 recording、时长、大小）、各阶段耗时、HTTP 状态码、第三方响应原文（截断）。
3. 错误分类准确：区分网络超时、客户端错误（4xx）、服务端错误（5xx）、业务失败（任务状态 failed），不再笼统归为 `CLIENT_ERROR`。
4. 不引入新的告警/监控系统，仅通过结构化日志达成（沿用现有日志通道）。

## 需求详述

### 1. 分阶段日志

FunASR 转写链路拆为可辨识的阶段（上传/提交任务/轮询状态/获取结果），每个阶段的进入与退出都有日志：

- 阶段开始：`Debug` 级，含 taskId（或录制 ID）、阶段名。
- 阶段完成：`Information` 级，含阶段名、耗时、关键结果（如任务状态、结果条数）。
- 阶段失败：`Warning`/`Error` 级，含阶段名、耗时、异常或第三方错误详情。

### 2. 失败上下文

任何失败日志必须包含：

- **关联标识**：转写任务 ID、FunASR taskId、来源音频标识（recording id / 文件名）。
- **音频元数据**：音频时长、字节数、格式（若可得）。
- **第三方细节**：HTTP 状态码、FunASR 返回的错误码与 message 原文、响应 body 摘要（截断至合理长度，如 512 字符，避免刷爆日志）。
- **耗时**：从阶段开始到失败的耗时，以及任务总耗时。

### 3. 错误分类

异常消息中的分类标签必须反映真实原因：

| 分类 | 含义 |
|---|---|
| `NETWORK_TIMEOUT` | 请求第三方超时（连接/读取超时） |
| `CLIENT_ERROR` | 第三方返回 4xx（参数、鉴权、配额等） |
| `SERVER_ERROR` | 第三方返回 5xx 或任务状态为 failed 且原因在服务端 |
| `TASK_FAILED` | FunASR 任务状态为 failed 且带业务错误码 |
| `UNKNOWN` | 无法归类的其他异常 |

「request timeout after 23 seconds」这类现象应归类为 `NETWORK_TIMEOUT`（或按实际原因归类），而不是 `CLIENT_ERROR`。

### 4. 结果校验日志

转写「成功」但结果为空（`segments=0`）属于可疑结果，需单独以 `Warning` 记录，并附带音频时长与任务状态，便于发现「静默空转写」。

## 验收标准

| # | 场景 | 预期 |
|---|------|------|
| 1 | FunASR 轮询返回任务 failed | 日志含 taskId、FunASR 错误码与 message 原文、阶段名（轮询）、音频元数据 |
| 2 | 请求 FunASR 超时 | 异常分类为 `NETWORK_TIMEOUT`，日志含超时耗时、目标阶段；不再出现笼统的 `CLIENT_ERROR` |
| 3 | FunASR 返回 4xx/5xx | 日志含 HTTP 状态码与响应 body 摘要（≤512 字符） |
| 4 | 转写完成但 segments=0 | 单独 `Warning` 日志，含音频时长与 taskId |
| 5 | 任意一次失败 | 仅凭日志即可定位失败阶段与第三方原因，无需复现 |
| 6 | 正常成功路径 | 日志量不明显增加（阶段完成日志为 Information，阶段进入为 Debug） |

## 非目标

- 不改变转写流程与重试策略（如确需调整重试，另立需求）。
- 不接入 metrics / tracing / 告警平台。
- 不更换 ASR 供应商，不修改 FunASR 部署。
- 不处理「空转写」的业务补偿（只要求可观测）。

## 依赖与风险

- 依赖 FunASR API 在失败时返回足够信息（错误码/message）；若某些路径只返回空响应，以 HTTP 状态码 + 耗时兜底。
- 响应 body 摘要必须截断，防止超大响应刷爆日志。
- 日志级别划分需遵守：成功路径不打 `Warning`，避免噪音淹没真实问题。
