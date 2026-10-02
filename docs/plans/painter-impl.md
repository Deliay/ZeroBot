# 小画家绘图插件（ZeroBot.Painter）—— 实现方案（Implementation Plan）

| 属性 | 值 |
|---|---|
| 需求分支 | `feat/painter-plugin` |
| 所属插件 | 新增 ZeroBot.Painter |
| 关联文档 | [PRD](./painter-prd.md) · [技术方案](./painter-tech.md) |
| 参照实现 | [synthesize-voice-impl.md](./synthesize-voice-impl.md)（Synthesize 热加载配置 / 额度扣减）；`ZeroBot.ComfyUI/ToAkumaria`（`CommandQueuedHandler` + 表情反馈）；[notify-voice-broadcast-impl.md](./notify-voice-broadcast-impl.md)（impl 文档体例） |

---

## 1. 目标

落地 PRD 全部功能：群内三条指令 `/小画家:启用:{number}`（管理）、`/小画家:禁用`（管理）、`/小画家:画:{prompt}`（绘图）；未开启的群对绘图指令完全静默；失败调用计入当日用量；绘图接口使用独立长超时 HTTP 客户端。

技术路线严格按技术方案执行：

- 新增插件工程 **`ZeroBot.Painter`**（`PainterPlugin` / `PainterOptions` / `PainterApi` / `PainterQuota` / `PainterManageCommandHandler` / `PaintCommandHandler`），依赖仅 `ZeroBot.Abstraction` + `ZeroBot.Utility`。
- 配置 `painter-config.json` 热加载：endpoint、HTTP 超时、群配置表、当日用量表。
- 限流：群（PeerId）+ 发送人（SenderId）、UTC+8 自然日；**先消耗后调用、失败不回滚**。
- HTTP 超时：独立 `HttpClient` + **请求级** `CancellationTokenSource`，超时热生效，默认 600 秒。
- prompt 原始文本解析，保留冒号 / 连字符等字符（同 `/学`）。

## 2. 前置确认（已核实）

- 需求总分支 `feat/painter-plugin` 基于 main（`fffba59`），HEAD `250e580`（PRD 与技术方案已入库），与 `origin/feat/painter-plugin` 同步，工作区干净。
- 逐条读码核对技术方案「现状分析」，结论一致，另发现 **1 处技术方案指针与 PRD 验收冲突**（见下表最后一行，本方案已修正）：

| 依赖点 | 核实结论 |
|---|---|
| 命令分发 | `CommandDispatcher.RunCommandDispatcherAsync` 对一条消息 `FirstOrDefaultAsync(predicate)` 只命中一个 handler，未命中则**完全无响应**（`src/ZeroBot.Core/Services/CommandDispatcher.cs:37-45`）→ 「未开启群静默」由绘图 predicate `return false` 天然实现 |
| 队列 | `CommandQueuedHandler`：`HandleAsync` = `EnqueueInspectorAsync` + 入队，`DequeueAsync` 串行消费（`src/ZeroBot.Utility/CommandQueuedHandler.cs`） |
| 表情反馈 | `@event.AddReaction(bot, KnownReactionEmojiIds.Click, ct)` / `RemoveReaction(...)`（`src/ZeroBot.Utility/ReactionExtensions.cs`）；`ToAkumaria.DequeueAsync` 的 `finally` 移除表情（`src/plugins/ZeroBot.ComfyUI/ToAkumaria.cs:40-44`） |
| 权限 | `permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "painter.manage", ct)`（`src/ZeroBot.Utility/ChatPermissionExtensions.cs:32`） |
| 配置读写 | `config.BeginConfigMutationScopeAsync(async (value, token) => { ...; await config.SaveAsync(value, token); }, ct)`（`src/plugins/ZeroBot.Synthesize/DatasetAliasCommandHandler.cs:64-70`）；`IJsonConfig<T>` 另有 `Current` / `WaitForInitializedAsync` |
| 配置字段大小写 | `JsonConfig.SaveAsync` / `JsonFileContentWatcher` 均用 `System.Text.Json` **默认选项**（无 NamingPolicy、区分大小写）→ 落盘与解析的字段名一律 **PascalCase**（`{"Date":"2026-10-02","Count":2}`）；技术方案 JSONC 示例的 camelCase 仅是文档示意，手写 camelCase 字段会被静默忽略 |
| 额度模式 | `SynthesizeQuota.Today/Key`（UTC+8 日期、`{peerId}:{senderId}` key）→ 复制同模式为 `PainterQuota`（`src/plugins/ZeroBot.Synthesize/SynthesizeQuota.cs`） |
| 图片发送 | `byte[].ToMilkyImageSegment()` → `ImageOutgoingSegment(base64://...)`（`src/ZeroBot.Utility/EventExtensions.cs`） |
| prompt 解析 | 参照 `SynthesizeCommandHandler.TryParse`（`/学`）：命令前缀之后首个分隔符之后的内容整体保留（`src/plugins/ZeroBot.Synthesize/SynthesizeCommandHandler.cs:78-95`） |
| 测试工程 | `test/ZeroBot.Core.Test` 经 `ZeroBot.Core` 传递引用各插件工程（`SynthesizeQuotaTest` 已用 `using ZeroBot.Synthesize;` 验证），可直接测 `ZeroBot.Painter` 纯函数 |
| **多图提取（修正技术方案）** | 技术方案指针 `GetMilkyImageMessagesAsync(bot).Take(MaxImages)` **不能满足 PRD**：该方法对单条消息只 `yield` 「回复消息首图 + 本消息**首张**图」（最多 2 张，`src/ZeroBot.Utility/EventExtensions.cs:120-146`），而 PRD 3.3 要求「随消息的图片（可能有多张）都传入」、验收 5 要求「附 2 张 → 2 个 `image` 字段；附 12 张 → 10 个」。**修正**：直接枚举 `message.Data.Segments.OfType<ImageIncomingSegment>()` 按出现顺序取前 `MaxImages` 张；同时**不**引入「回复消息图片」语义（PRD 定义的输入是「随消息的图片」，行为以验收 5 为准） |

## 3. 实施步骤

按依赖顺序执行，每步完成后 `dotnet build ZeroBot.slnx` 通过再进入下一步；新建插件流程按 `src/plugins/AGENTS.md`「创建新插件」。

### Step 1：新增插件工程 `src/plugins/ZeroBot.Painter/ZeroBot.Painter.csproj`（新增）

对齐 `src/plugins/AGENTS.md` 模板，仅引用 Abstraction + Utility（`Milky.Net.Model` 经 Abstraction 传递），**无第三方包、无 `Directory.Packages.props` 改动**：

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\ZeroBot.Abstraction\ZeroBot.Abstraction.csproj" />
        <ProjectReference Include="..\..\ZeroBot.Utility\ZeroBot.Utility.csproj" />
    </ItemGroup>
</Project>
```

### Step 2：配置模型 —— `PainterOptions.cs`（新增）

```csharp
namespace ZeroBot.Painter;

/// <summary>每日用量记录。日期按 UTC+8 计算，格式 yyyy-MM-dd。</summary>
public record DailyQuota(string Date, int Count);

/// <summary>小画家绘图插件配置（热加载 painter-config.json）。</summary>
public record PainterOptions
{
    public const string DefaultEndpoint = "http://z-vtuber-training.vtuber.svc.cluster.local:8080";
    public const int DefaultHttpTimeoutSeconds = 600;   // 10 分钟
    public const int DefaultMaxImages = 10;
    public const int DefaultMaxDailyLimit = 5;

    /// <summary>绘图服务地址，热加载。</summary>
    public string Endpoint { get; init; } = DefaultEndpoint;

    /// <summary>绘图接口专用超时（秒），默认 600；每次请求读取当前值，改配置即热生效。</summary>
    public int HttpTimeoutSeconds { get; init; } = DefaultHttpTimeoutSeconds;

    /// <summary>单次绘图最多携带的参考图数量，超出部分忽略。</summary>
    public int MaxImages { get; init; } = DefaultMaxImages;

    /// <summary>启用指令 {number} 的上限（PRD：1~5）。</summary>
    public int MaxDailyLimit { get; init; } = DefaultMaxDailyLimit;

    /// <summary>群 PeerId → 每人每日上限；存在即表示该群已开启。由 /小画家:启用、/小画家:禁用 维护。</summary>
    public Dictionary<long, int> Groups { get; init; } = [];

    /// <summary>"{peerId}:{senderId}" → 当日用量，持久化跨重启；禁用群时不清除。</summary>
    public Dictionary<string, DailyQuota> DailyQuotas { get; init; } = [];

    public static PainterOptions Default => new()
    {
        Endpoint = Environment.GetEnvironmentVariable("Z_VTUBER_TRAINING_ENDPOINT") ?? DefaultEndpoint
    };
}
```

- 磁盘字段名即属性名（PascalCase，见前置确认），实际落盘形如：

  ```json
  {
    "Endpoint": "http://z-vtuber-training.vtuber.svc.cluster.local:8080",
    "HttpTimeoutSeconds": 600,
    "MaxImages": 10,
    "MaxDailyLimit": 5,
    "Groups": { "123456": 3 },
    "DailyQuotas": { "123456:7890": { "Date": "2026-10-02", "Count": 2 } }
  }
  ```

- `Endpoint` 环境变量回落仅在**无配置文件**时生效（与 `SynthesizeOptions.Default` 完全一致）；文件存在后以文件值为准（含热加载）。

### Step 3：额度与校验纯函数 —— `PainterQuota.cs`（新增）

```csharp
namespace ZeroBot.Painter;

/// <summary>每日用量与日期计算工具，纯函数，便于单元测试。</summary>
public static class PainterQuota
{
    /// <summary>UTC+8（北京时间），中国无夏令时。</summary>
    public static readonly TimeSpan ChinaOffset = TimeSpan.FromHours(8);

    /// <summary>按 UTC+8 计算当前日期，每天 0 点重置。</summary>
    public static DateOnly Today(DateTimeOffset now) =>
        DateOnly.FromDateTime(now.ToOffset(ChinaOffset).DateTime);

    /// <summary>用量 key：群 PeerId + 发送人 SenderId。</summary>
    public static string Key(long peerId, long senderId) => $"{peerId}:{senderId}";

    /// <summary>启用指令 number 合法性：1 &lt;= number &lt;= max。</summary>
    public static bool IsValidDailyLimit(int number, int max) => number >= 1 && number <= max;
}
```

### Step 4：绘图接口客户端 —— `PainterApi.cs`（新增）

构造注入 `ILogger<PainterApi>`；**自持专用 `HttpClient`（静态，不注册进容器）**：

```csharp
// 专用实例：Timeout 置为 Infinite，唯一超时来源是请求级 CTS（可热加载）；
// 也不与 SynthesizePlugin 注册的 AddSingleton<HttpClient>() 抢解析（插件容器合并语义未定义）。
private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
```

`GenerateAsync`（签名对齐技术方案，超时由调用方从热加载配置取快照传入）：

```csharp
public async Task<byte[]?> GenerateAsync(string endpoint, string prompt, IReadOnlyList<byte[]> images,
    TimeSpan timeout, CancellationToken cancellationToken = default)
```

实现要点：

1. `using var form = new MultipartFormDataContent();`
   - `form.Add(new StringContent(prompt), "text");`（值即 prompt 原文，**不加引号**——curl 示例里的引号是导出工具/外壳语法）
   - 每张图：按**文件魔数**嗅探类型（PNG/JPEG/GIF/WebP/BMP，未知回落 `application/octet-stream`），
     `form.Add(new ByteArrayContent(img), "image", $"image-{i}.{ext}");` 并显式设置对应的 `Content-Type`——
     不能一律声称为 `image/jpeg`，否则训练服务按声明类型解码 PNG/WebP 等会产生本可避免的失败（而失败会计入用户额度）
2. 请求级超时（热生效）：

   ```csharp
   using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
   cts.CancelAfter(timeout);
   var response = await Http.PostAsync($"{endpoint.TrimEnd('/')}/api/training/images/generate", form, cts.Token);
   ```

3. 失败一律返回 `null` 并 `LogError`（同 `SynthesizeApi` 约定）：
   - 非 2xx；`TaskCanceledException` 且外层 `cancellationToken` 未取消（= 超时，日志注明超时秒数）；其他异常；空响应体；
   - **防御分支**：`Content-Type` 为 `application/json` → 记日志返回 `null`（技术方案风险项：接口若改为 JSON 结构响应，不在本期解析，避免把 JSON 当图片发出）。
4. 成功返回响应二进制（假定原始图片字节，技术方案风险项已知）。

### Step 5：绘图指令 —— `PaintCommandHandler.cs`（新增）

`CommandQueuedHandler`，构造注入 `ICommandDispatcher / IBotContext / IJsonConfig<PainterOptions> / PainterApi / ILogger<PaintCommandHandler>`。

**谓词（未开启群静默的关键）**：

```csharp
protected override async ValueTask<bool> PredicateAsync(Event<IncomingMessage> message, CancellationToken ct)
{
    if (message.Scene != MessageScene.Group) return false;          // 仅群聊
    if (!TryParsePrompt(message.ToText().Trim(), out _)) return false;  // 非本指令
    return config.Current.Groups.ContainsKey(message.Data.PeerId);  // 未开启 → false → 完全静默
}
```

**prompt 原始文本解析（public static，供单测）**：

```csharp
private const string CommandPrefix = "/小画家:画";
private const string CommandPrefixFullWidth = "/小画家：画";

/// <summary>
/// 解析 /小画家:画:{prompt}；prompt 为首个分隔符（: 或 ：）之后的全部原文，保留冒号/连字符。
/// 命中指令但无 prompt（如 "/小画家:画"、"/小画家:画:"）返回 true 且 prompt 为空；
/// "/小画家:画册..." 之类不是本指令，返回 false。
/// </summary>
public static bool TryParsePrompt(string raw, out string prompt)
```

- 分隔符只认 `:` 与 `：`；`-` **不**作为本指令分隔符（prompt 里的连字符须保留）。
- `raw` 取 `message.ToText().Trim()`（整体 Trim，与 `/学` 一致）；prompt 中间内容原样保留。

**`DequeueAsync` 流程**（对应 PRD 3.3）：

1. **进入消费阶段第一步**：从「发起时上限」表 `TryTake` 取出并移除本请求的 `limit`（见下「发起时状态」）；取不到仅记 `LogWarning` 兜底返回。**该释放先于所有分支**，因此解析失败、空 prompt、额度用尽、正常处理等任何路径都不会残留记录（`PaintRequestLimits` 独立类型，`Count` 可断言无残留）。
2. `TryParsePrompt` 失败 → 回复 Help（理论上谓词已挡，防御性分支）；`prompt` 空白 → 回复「提示词不能为空…」，**不计数、不调用接口**。
3. 限流：`TryConsumeQuotaAsync(peerId, senderId, limit, ct)`（`limit` 即第 1 步取出值，不再重读 `config.Current.Groups`）失败 → 回复「你今天在本群的绘图次数已用完（{limit} 张），明天再来吧」，不调用接口。
4. 取图 + 调用接口（**统一包一层 try/catch，异常仅记日志并按失败处理**）：

   ```csharp
   foreach (var seg in @event.Data.Segments.OfType<ImageIncomingSegment>().Take(options.MaxImages))
       images.Add(await seg.GetMilkyImageBytesAsync(bot, @event, ct));
   var bytes = await api.GenerateAsync(options.Endpoint, prompt, images,
       TimeSpan.FromSeconds(options.HttpTimeoutSeconds), ct);
   ```

5. `bytes is null`（或第 4 步异常）→ 回复「绘图失败，请稍后重试。」；**用量已消耗，不回滚**。
6. 成功 → `await @event.ReplyAsGroup(bot, ct, [bytes.ToMilkyImageSegment()]);`（回复原消息）。
7. 整体 `catch (Exception e) => logger.LogError(...)` 兜底；`finally` 中移除表情，且**移除动作自身包一层 try/catch**（`LogWarning` 兜底）——`finally` 内异常会直接逃出 `DequeueAsync`，而 `CommandQueuedHandler` 的串行消费循环没有 try/catch，一旦抛出会永久中断绘图队列，此后所有绘图静默失效直至重启。

**「发起时状态」与并发正确性（修正方案遗留冲突）**：

- PRD §5 要求「开启状态修改/关闭与正在进行的绘图并发时，以发起时状态为准，进行中的调用完成并回发结果」；而原方案「Dequeue 时重读 Groups，取不到直接 return」会让排队期间被禁用的请求在贴过「处理中」表情后彻底静默，直接违反该条。
- 修正：覆写 `HandleAsync`，在**入队（贴表情 + 写队列）之前**从 `Groups[peerId]` 捕获发起时上限，存入 `PaintRequestLimits`（内部 `ConcurrentDictionary<(long PeerId, long MessageSeq), int>`）；`DequeueAsync` **入口第一步**以 `(peerId, messageSeq)` `TryTake` 取出即移除后使用该值。队列等待期间管理员禁用本群或调低上限，均不影响在途请求：请求照常扣额、调用接口并回发结果/失败提示。
- 派发谓词仍负责「未开启群静默」（验收 3）：未开启群在派发阶段即 `return false`，不会贴表情、不入队。
- 若 `HandleAsync` 时发现群已在谓词与处理之间被关闭（极端窄窗口），则静默不入队；`base.HandleAsync` 抛异常时以 `TryTake` 清理已记录的上限，避免状态泄漏。`DequeueAsync` 入口即取出释放，任何后续分支都不会残留（长驻进程无内存增长）。

**限流（先消耗、失败不回滚）**：

```csharp
private async ValueTask<bool> TryConsumeQuotaAsync(long peerId, long senderId, int limit, CancellationToken ct)
{
    var key = PainterQuota.Key(peerId, senderId);
    var today = PainterQuota.Today(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
    return await config.BeginConfigMutationScopeAsync(async (value, token) =>
    {
        if (!value.DailyQuotas.TryGetValue(key, out var quota) || quota.Date != today)
            value.DailyQuotas[key] = quota = new DailyQuota(today, 0);
        if (quota.Count >= limit) return false;
        value.DailyQuotas[key] = quota with { Count = quota.Count + 1 };
        await config.SaveAsync(value, token);
        return true;
    }, ct);
}
```

- 与 `SynthesizeCommandHandler.TryConsumeQuotaAsync` 同构，但**不提供 `ReleaseQuotaAsync`、失败不回滚**（PRD 明确要求失败计入用量；synthesize 的失败回滚行为不可照抄）。
- mutation scope 内「检查并自增」原子完成，同群同人并发不会超扣。

**表情（处理中）**：

```csharp
protected override ValueTask EnqueueInspectorAsync(Event<IncomingMessage> @event, CancellationToken ct)
    => @event.AddReaction(bot, KnownReactionEmojiIds.Click, ct);
```

### Step 6：管理指令 —— `PainterManageCommandHandler.cs`（新增）

`CommandHandler`（配置读写快，无需排队），构造注入 `ICommandDispatcher / IPermission / IBotContext / IJsonConfig<PainterOptions>`，结构对齐 `DatasetAliasCommandHandler`。

- 谓词：`message.Scene == MessageScene.Group` 且 `message.ToTextCommands().FirstOrDefault()` 解析出 `Name == "小画家"` 且 `Arguments[0] is "启用" or "禁用"`（用指令解析匹配，天然兼容全角 `：` 与 `-` 分隔），且 `permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "painter.manage", ct)`。
  - 无权限 / 私聊 → 谓词 false → 静默（PRD 验收 9「按现有权限模型的既有行为」）。
  - 前缀与 `/小画家:画`、`/synthesize`、`/学` 等互斥，不抢占分发。
- `HandleAsync`（`peerId = message.Data.PeerId`）：

  | Arguments | 行为 |
  |---|---|
  | `["启用", number, ...]` | `int.TryParse(number)` 且 `PainterQuota.IsValidDailyLimit(n, options.MaxDailyLimit)` → mutation scope 内 `Groups[peerId] = n` → `SaveAsync` → 回复「已开启本群绘图功能，每人每天最多绘制 {n} 张」；不合法（非数字 / <1 / >5 / 缺参）→ 回复参数错误提示，**不写配置**。多余参数忽略。重复开启 = 覆盖上限（幂等），**当日已消耗计数不重置** |
  | `["禁用", ...]` | mutation scope 内 `Groups.Remove(peerId)` → `SaveAsync` → 回复「已关闭本群绘图功能」；未开启过也回复确认（幂等不报错）。**只删 Groups，不清 `DailyQuotas`**（PRD 3.2：关闭后同日重开计数仍有效，防刷额度） |

- Help 文本：

  ```
  /小画家:启用:{number}（每人每日上限，1~5）
  /小画家:禁用
  ```

### Step 7：插件入口 —— `PainterPlugin.cs`（新增）

```csharp
public class PainterPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();
        services.ConfigureJsonConfig("painter-config.json", PainterOptions.Default, cancellationToken);
        services.AddSingleton<PainterApi>();
        services.AddSingletonComponent<PainterManageCommandHandler>();
        services.AddSingletonComponent<PaintCommandHandler>();
        return ValueTask.FromResult(services);
    }
}
```

- 无 `IWithInitializer`、无 `IServiceManager` 注册（全部依赖都在本插件容器内，无跨插件契约）。
- **不注册 `HttpClient` 到容器**（`PainterApi` 自持专用实例，见 Step 4）。

### Step 8：构建接入（改）

1. `src/ZeroBot.Core/ZeroBot.Core.csproj`：`<ItemGroup>` 增加

   ```xml
   <ProjectReference Include="..\plugins\ZeroBot.Painter\ZeroBot.Painter.csproj" />
   ```

2. `src/ZeroBot.Core/Program.cs`：`using ZeroBot.Painter;` + `TypedPluginLoader.Register<PainterPlugin>();`（追加在 `SynthesizePlugin` 注册之后）。
3. `src/ZeroBot.Core/Dockerfile`：插件 csproj COPY 段补一行（供容器构建 restore，与既有条目同模式）：

   ```dockerfile
   COPY ["src/plugins/ZeroBot.Painter/ZeroBot.Painter.csproj", "src/plugins/ZeroBot.Painter/"]
   ```

4. `ZeroBot.slnx`：`/src/plugins/` 分组按字母序附近加入

   ```xml
   <Project Path="src/plugins/ZeroBot.Painter/ZeroBot.Painter.csproj" />
   ```

### Step 9：文档 —— `src/plugins/AGENTS.md`（改）

「现有插件列表」新增 `ZeroBot.Painter` 条目：

- 路径 `src/plugins/ZeroBot.Painter/`、命名空间 `ZeroBot.Painter`；
- 组件：`PainterManageCommandHandler`（启用/禁用指令）、`PaintCommandHandler`（绘图指令）、`PainterApi`（绘图接口客户端）、`PainterQuota`（UTC+8 每日用量工具）；
- 命令：`/小画家:启用:{number}` / `/小画家:禁用`（仅群聊，高权限用户，权限 `painter.manage`）、`/小画家:画:{prompt}`（仅群聊，附 0~10 张图，受群内每人每日上限限制）；
- 配置文件：`painter-config.json`（`endpoint` 默认可用环境变量 `Z_VTUBER_TRAINING_ENDPOINT`；`HttpTimeoutSeconds` 默认 600；`Groups` 群配置表；`DailyQuotas` 当日用量表）。

### Step 10：单元测试 —— `test/ZeroBot.Core.Test/`（新增）

纯函数覆盖（xunit，风格参照 `SynthesizeQuotaTest`）：

- `PainterQuotaTest.cs`：
  - `Today`：UTC+8 日期翻转（`2026-09-30T16:00Z` → `2026-10-01`；`2026-09-30T15:59:59Z` → `2026-09-30`）；
  - `Key`：`{peerId}:{senderId}` 格式；
  - `IsValidDailyLimit` 理论：`0/1/5/6` 与负数边界（`max=5` 时 0/6/负数 → false，1/5 → true；`max=1` 时 1 → true、2 → false）。
- `PaintCommandHandlerTest.cs`（`PaintCommandHandler.TryParsePrompt`）：
  - `/小画家:画:一只在樱花树下打盹的猫` → `一只在樱花树下打盹的猫`；
  - prompt 含冒号 / 连字符 / 空格原样保留：`/小画家:画:a:b-c d` → `a:b-c d`；
  - 全角分隔符 `/小画家：画：一只猫` → `一只猫`；
  - `/小画家:画` 与 `/小画家:画:` → 命中但 prompt 空；
  - `/小画家:画册:x`、`/变毬` → 不命中（false）。

## 4. 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/plugins/ZeroBot.Painter/ZeroBot.Painter.csproj` | 新增 |
| `src/plugins/ZeroBot.Painter/PainterPlugin.cs` | 新增（插件入口与注册） |
| `src/plugins/ZeroBot.Painter/PainterOptions.cs` | 新增（热加载配置模型） |
| `src/plugins/ZeroBot.Painter/PainterApi.cs` | 新增（multipart 绘图客户端 + 请求级超时） |
| `src/plugins/ZeroBot.Painter/PainterQuota.cs` | 新增（额度 / 日期 / 上限校验纯函数） |
| `src/plugins/ZeroBot.Painter/PainterManageCommandHandler.cs` | 新增（启用 / 禁用指令） |
| `src/plugins/ZeroBot.Painter/PaintCommandHandler.cs` | 新增（绘图指令，`CommandQueuedHandler`） |
| `src/plugins/ZeroBot.Painter/PaintRequestLimits.cs` | 新增（发起时上限暂存表，入队记录 / 消费入口取出即移除） |
| `src/ZeroBot.Core/ZeroBot.Core.csproj` | 改：新增 ProjectReference |
| `src/ZeroBot.Core/Program.cs` | 改：注册 `PainterPlugin` |
| `src/ZeroBot.Core/Dockerfile` | 改：新增插件 csproj 的 COPY |
| `ZeroBot.slnx` | 改：`/src/plugins/` 分组加入新插件工程 |
| `src/plugins/AGENTS.md` | 改：新增 `ZeroBot.Painter` 条目 |
| `test/ZeroBot.Core.Test/PainterQuotaTest.cs` | 新增 |
| `test/ZeroBot.Core.Test/PaintCommandHandlerTest.cs` | 新增 |
| `test/ZeroBot.Core.Test/PainterApiTest.cs` | 新增 |
| `test/ZeroBot.Core.Test/PaintRequestLimitsTest.cs` | 新增 |
| `docs/plans/painter-impl.md` | 新增（本文档） |

**不改 `ZeroBot.Abstraction` / `ZeroBot.Utility` / 任何既有插件**；无新增 NuGet 依赖、无 `Directory.Packages.props` 改动；**不改动任何线上配置文件**（`painter-config.json` 由插件自身在运行目录 `~/Projects/Bot/ZeroBot` 首次写入时自动生成）。

## 5. 验证

1. 构建：`dotnet clean src/ZeroBot.Core/ZeroBot.Core.csproj -c Release && dotnet publish src/ZeroBot.Core/ZeroBot.Core.csproj -c Release`（按部署手册，**不得加 `-o`**）编译通过、无新增告警。
2. 测试：`dotnet test test/ZeroBot.Core.Test/ZeroBot.Core.Test.csproj -c Release` 全绿（含本次新增纯函数用例 + 既有回归）。
3. 部署（`pm2 restart ZeroBot --update-env`，**严禁 `pm2 delete`**）后对照 PRD 第 7 节 12 条验收标准执行，重点：
   - `/小画家:启用:3`（有权限）→ 确认回复 + `painter-config.json` 出现 `"Groups": { "该群": 3 }`；`/小画家:启用:6`、`/小画家:启用:abc`、`/小画家:启用` → 参数错误回复且配置未变；无权限用户 → 无响应；
   - 未开启的群发 `/小画家:画:test` → **无任何响应**（无回复、无表情、无计数、无接口调用）；
   - 已开启群 `/小画家:画:一只猫` → 贴处理中表情 → 收到生成图片（回复原消息）→ 表情移除；附 2 张图 → 接口收到 2 个 `image` 字段；附 12 张 → 只发 10 个；
   - 上限 1 的群：首次成功后再次发送 → 额度用完回复；UTC+8 次日 0 点恢复；
   - 模拟接口 500 / 长时间不响应 → 失败提示（超时按 `HttpTimeoutSeconds` 生效），**再次发送时计数已 +1**（失败计入用量、不回滚），期间不阻塞其他命令（队列串行但不卡 dispatcher）；
   - `/小画家:禁用` 后绘图静默；重复禁用不报错；关闭后同日重开 → 旧计数仍有效；
   - 热加载：改 `painter-config.json` 的 `HttpTimeoutSeconds` / `Endpoint` 无需重启即生效；
   - 重启 Bot → 开启状态与当日用量自动恢复。

## 6. 备注

- **「以发起时状态为准」的落地**：群上限在 `HandleAsync`（入队前）捕获，`DequeueAsync` 不再重读 `Groups`，从而修复原方案「排队期间禁用则静默丢弃」与 PRD §5 / §3.1 的冲突（详见 Step 5）。
- **队列健壮性**：`DequeueAsync` 的 `finally` 中移除「处理中」表情必须自带 try/catch，否则表情接口异常会中断 `CommandQueuedHandler` 的串行消费循环，导致绘图功能整体失效直至重启（存量 `ToAkumaria` 亦有此隐患，但本 PR 按「不改任何既有插件」约定仅修复新插件）。
- **参考图类型嗅探**：参考图按魔数设置 `Content-Type` 与扩展名，避免把 PNG/WebP 等错误声明为 jpeg 导致接口失败并误扣用户额度。
- **失败不回滚是与 Synthesize 的关键差异**：`/学` 失败会 `ReleaseQuotaAsync` 返还额度，小画家**不返还**（需求明确要求）；实现时勿照抄 synthesize 的回滚段落。
- **表情时序**：按技术方案在 `EnqueueInspectorAsync` **入队即贴**「处理中」表情（让用户感知排队），`DequeueAsync` 的 `finally` 移除；与 PRD「消耗额度后贴表情」的唯一差异是「额度已用尽」的请求会短暂出现表情后移除（PRD 只对未开启群要求「不贴表情」，该场景由谓词保证完全静默）。
- **多图取自本消息**：不复用 `GetMilkyImageMessagesAsync`（单消息只取首图且含回复图语义），改为枚举消息自身全部 `ImageIncomingSegment` 取前 `MaxImages` 张，精确对齐 PRD 验收 5；「回复某张图再画」的回复图语义本期不做，如需追加是 3 行改动。
- **JSON 字段 PascalCase**：`painter-config.json` 由 `JsonConfig` 默认序列化读写，字段名与 record 属性同名；技术方案示例中的 camelCase 仅是文档示意（手写 camelCase 不生效），运维手改配置时须用 PascalCase。
- **prompt 分隔符**：只认 `:` / `：`，`-` 不作分隔符（保留连字符）；与 `ToTextCommands` 的默认分隔符集合（`:：-`）不同，属刻意差异。
- **`DailyQuotas` 不清理历史日期**（与 `SynthesizeOptions.DailyQuotas` 一致）：长期运行会积累少量旧记录（每群每人每天 1 条），单条体积小，本期接受；后续可在保存时顺手清理非当日记录。
- **响应形态风险**（技术方案已列）：本期假定接口返回原始图片二进制；若实际为 JSON，`PainterApi` 的 `application/json` 防御分支会按失败处理（不发坏数据），届时再补解析分支。
- **长耗时排队**：`PaintCommandHandler` 串行消费，接口慢时同群绘图请求排队，属刻意背压设计；管理指令在独立 handler，不受绘图队列阻塞。
