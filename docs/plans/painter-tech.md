# 技术提案：小画家绘图插件（ZeroBot.Painter）

- 日期：2026-10-02
- 状态：待评审
- 关联文档：[painter-prd.md](painter-prd.md)（产品需求）

## 背景

后台训练服务 `vtuber-training` 提供图像生成接口：

```
POST {endpoint}/api/training/images/generate
Content-Type: multipart/form-data

text={提示词}
image=@1.jpg   # 可重复，最多 10 张
```

需要新增插件提供群聊绘图指令：`/小画家:启用:{number}`（管理）、`/小画家:禁用`（管理）、`/小画家:画:{prompt}`（绘图）。未开启的群对绘图指令完全静默；失败调用计入当日用量；接口耗时长，需要较长的 HTTP 超时。

## 需求

| # | 需求 |
|---|------|
| R1 | 管理指令 `/小画家:启用:{number}`（1~5）与 `/小画家:禁用`，仅群聊，高权限用户（sudoers / 群管理员 / `painter.manage` 权限）可用，配置持久化 |
| R2 | 绘图指令 `/小画家:画:{prompt}`，仅群聊；未开启的群完全静默（不回复、不贴表情、不计数） |
| R3 | 消息附带图片 0~10 张与 prompt 一起以 multipart/form-data 提交绘图接口，结果以图片回发群聊 |
| R4 | 限流：群（PeerId）+ 发送人（SenderId）每天（UTC+8）上限为开启时的 number；**失败调用也计入**，消耗发生在调用接口之前 |
| R5 | 绘图接口地址与 HTTP 超时动态可配（JSON 配置，热加载），超时默认 10 分钟 |
| R6 | prompt 采用原始文本解析，保留冒号/连字符等字符（同 `/学`） |

## 现状分析

项目已有可复用的模式：

- `IPlugin` + `IServiceCollection` 注册组件、热加载配置（`ConfigureJsonConfig`），参考 `SynthesizePlugin`。
- `CommandQueuedHandler`：串行处理命令，避免同群并发绘图打爆后端；`EnqueueInspectorAsync` 贴表情、`finally` 移除表情的模式见 `ZeroBot.ComfyUI.ToAkumaria`。
- 图片提取：`@event.GetMilkyImageMessagesAsync(bot)` + `image.GetMilkyImageBytesAsync(bot, @event)`（`src/ZeroBot.Utility/EventExtensions.cs`），`ToAkumaria`、`PuzzleSolver` 均在使用。
- 权限：`ChatPermissionExtensions.IsSudoerOrGroupAdminOrHasPermissionAsync`。
- 配置持久化：`IJsonConfig<T>` 的 `BeginConfigMutationScopeAsync` + `SaveAsync`。
- 每日额度：`SynthesizeQuota.Today`/`Key` 的纯函数模式（UTC+8 日期、`{peerId}:{senderId}` key），本项目复制同模式为 `PainterQuota`。
- 图片发送：`ImageOutgoingSegment` 支持 `base64://`。
- `CommandDispatcher` 对一条消息只命中一个 handler（`FirstOrDefault`），管理指令与绘图指令的 `PredicateAsync` 必须互斥：管理指令匹配 `/小画家:启用` / `/小画家:禁用` 前缀，绘图指令匹配 `/小画家:画` 前缀，互不重叠。

## 方案设计

### 新增插件工程 `ZeroBot.Painter`

```
src/plugins/ZeroBot.Painter/
├── ZeroBot.Painter.csproj
├── PainterPlugin.cs                  # IPlugin，注册配置/组件/服务
├── PainterOptions.cs                 # 热加载配置模型
├── PainterApi.cs                     # 绘图接口 HTTP 客户端（multipart）
├── PainterQuota.cs                   # 每日额度与日期工具（纯函数，可测）
├── PainterManageCommandHandler.cs    # /小画家:启用 / /小画家:禁用
└── PaintCommandHandler.cs            # /小画家:画
```

依赖仅 `ZeroBot.Abstraction` 与 `ZeroBot.Utility`（`Milky.Net.Model` 经 Abstraction 传递）。

### 配置模型（`painter-config.json`）

```jsonc
{
  "endpoint": "http://z-vtuber-training.vtuber.svc.cluster.local:8080",
  "httpTimeoutSeconds": 600,
  "maxImages": 10,
  "maxDailyLimit": 5,
  "groups": { "123456": 3 },
  "dailyQuotas": { "123456:7890": { "date": "2026-10-02", "count": 2 } }
}
```

- `Endpoint`：默认取环境变量 `Z_VTUBER_TRAINING_ENDPOINT`（与 synthesize 同一服务），配置文件存在后以文件为准；热加载（R5）。
- `HttpTimeoutSeconds`：绘图接口专用 HttpClient 超时，默认 600 秒（10 分钟）；独立实例不复用其他插件的 HttpClient（R5）。
- `Groups`：群 PeerId → 每日上限；存在即表示该群已开启。启用/禁用指令通过 mutation scope 写入/移除并 `SaveAsync`。
- `MaxDailyLimit`：number 上限（5），配置化便于后续调整；启用指令校验 `1 <= number <= MaxDailyLimit`。
- `DailyQuotas`：`{peerId}:{senderId}` → `{date, count}`，持久化跨重启（R4）。关闭群配置时不清除对应计数，保证「关闭后同日重新开启计数仍有效」。

### 权限与路由

| 指令 | Predicate 条件 | 处理 |
|------|----------------|------|
| `/小画家:启用:{number}` / `/小画家:禁用` | 群聊 且 `StartsWith("/小画家:启用")` 或 `StartsWith("/小画家:禁用")` 且 `IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "painter.manage")` | 解析参数，校验 1~5，写入/移除群配置，回复确认 |
| `/小画家:画:{prompt}` | 群聊 且 `StartsWith("/小画家:画")` 且 **本群已开启**（`Groups.ContainsKey(peerId)`） | 限流检查 → 消耗计数 → 贴表情 → 调用接口 → 回发结果 |

- 「未开启的群完全静默」（R2）由绘图指令的 predicate 实现：未开启直接 `return false`，`CommandDispatcher` 不分发，自然无任何响应（不回复、不贴表情、不计数）。
- 私聊直接 `return false`，实现「仅群聊」。

### 绘图流程（`PaintCommandHandler`，`CommandQueuedHandler`）

1. 原始文本解析 prompt：定位 `/小画家:画` 之后的首个 `:`/`：`，其余全部作为 prompt 原文（R6）；为空 → 回复参数错误，不计数。
2. 限流（R4）：在配置 mutation scope 内原子「检查并自增」：
   - key = `{peerId}:{senderId}`，日期 = `PainterQuota.Today(DateTimeOffset.UtcNow)`（UTC+8）。
   - 记录日期非今日则重置 count 为 0；`count >= 群上限` → 不消耗，回复额度用完提示，`return`。
   - 否则 count + 1 并 `SaveAsync`（**先消耗，失败不回滚**）。
3. `EnqueueInspectorAsync` 贴 `KnownReactionEmojiIds.Click` 表情（同 `ToAkumaria`），`finally` 中移除。
4. 提取图片：`GetMilkyImageMessagesAsync(bot).Take(MaxImages)` 逐张 `GetMilkyImageBytesAsync` 下载。
5. 调用 `PainterApi.GenerateAsync`：
   ```csharp
   using var form = new MultipartFormDataContent();
   form.Add(new StringContent(prompt), "text");
   foreach (var img in images)
       form.Add(new ByteArrayContent(img), "image", $"image-{i}.jpg");
   var response = await http.PostAsync($"{endpoint}/api/training/images/generate", form, ct);
   ```
   - 非 2xx、超时（`TaskCanceledException`）、异常、空响应体 → 视为失败，回复失败提示并记录日志。
   - 成功 → 响应二进制经 `new ImageOutgoingSegment(new MilkyUri($"base64://{...}"))` 回发群聊。
6. 计数不回滚：接口失败属于预期路径，用量已消耗（需求明确要求）。

### HTTP 超时（R5）

- 插件注册**独立** `HttpClient` 实例：`new HttpClient { Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds) }`；超时值在发送请求时从热加载配置读取（每次请求构造请求级 `CancellationTokenSource`，或直接接受实例级超时在配置变更后需重启——取简单方案：请求时用 `CancellationTokenSource.CreateLinkedTokenSource(ct)` + `CancelAfter(当前配置超时)`，实现真正热生效）。
- 不用框架级 `cancellationToken` 直接取消长请求之外，绘图请求自身也受插件注销令牌约束，进程退出时可中断。

### 额度与日期工具（`PainterQuota`，纯函数）

```csharp
public static DateOnly Today(DateTimeOffset now);            // UTC+8 日期
public static string Key(long peerId, long senderId);        // "{peerId}:{senderId}"
public static bool IsValidDailyLimit(int number, int max);   // 1 <= number <= max
```

配套单元测试：UTC+8 日期翻转、key 格式、上限边界（0/1/5/6）。

## 影响面

| 文件 | 动作 |
|------|------|
| `src/plugins/ZeroBot.Painter/**` | 新增插件工程与源码 |
| `src/ZeroBot.Core/ZeroBot.Core.csproj` | 新增 ProjectReference |
| `src/ZeroBot.Core/Program.cs` | 注册 `PainterPlugin` |
| `src/ZeroBot.Core/Dockerfile` | 新增插件 csproj 的 COPY（供容器构建 restore） |
| `ZeroBot.slnx` | 加入解决方案 |
| `src/plugins/AGENTS.md` | 补充插件说明 |
| `docs/plans/painter-prd.md` / `painter-tech.md` | 新增方案文档 |
| `test/ZeroBot.Core.Test/PainterQuotaTest.cs` | 新增额度/日期单元测试 |

无数据库迁移、无其他插件改动；除新增 `painter-config.json` 外不改动任何部署配置。

## 风险

- **接口响应形态假设**：假设绘图接口返回原始图片二进制。若实际返回 JSON（如 URL/base64 字段）或多张图片，需在 `PainterApi` 增加解析分支。已通过「非 2xx/空体即失败」保证不会误发坏数据。
- **长耗时与队列堆积**：`CommandQueuedHandler` 串行处理同一 handler 的消息，接口慢时同群绘图请求会排队；这是刻意的背压设计，配合失败计数防滥用。处理中表情让用户感知排队状态。
- **图片下载开销**：每张参考图需经 Milky 下载为字节再上传，10 张大图会增加内存占用；按 `Take(10)` 截断 + 流式串行处理，可接受。
- **base64 体积**：生成图转 base64 发送与其他图片路径一致，可接受。
- **额度计数与并发**：mutation scope 内原子自增，同群同人并发绘图不会超扣；不同群互不影响。
