# 通知语音播报 —— 实现方案（Implementation Plan）

| 属性 | 值 |
|---|---|
| 需求分支 | `feat/notify-voice-broadcast` |
| 所属插件 | ZeroBot.Synthesize.Abstraction（新增库工程）、ZeroBot.Synthesize、ZeroBot.Bilibili、ZeroBot.Weibo |
| 关联文档 | [PRD](./notify-voice-broadcast-prd.md) · [技术方案](./notify-voice-broadcast-tech.md) |
| 参照实现 | [synthesize-voice-impl.md](./synthesize-voice-impl.md)（Synthesize 插件 / 配置写入 / `RecordOutgoingSegment`）；[bilibili-anchor-event-forward-impl.md](./bilibili-anchor-event-forward-impl.md)（Subscriber 挂载模式）；`ZeroBot.Endfield.Api`（库工程先例） |

---

## 1. 目标

落地 PRD 全部功能：群内指令 `/动态语音播报:启用:{alias}` / `/动态语音播报:禁用` 开关通知语音播报；开启后，该群已开启的 5 类通知（B 站开播、B 站动态、微博动态、直播间 SC、主播弹幕）在文字通知**之后**额外独立发送一条指定音色的语音。

技术路线严格按技术方案执行：

- 新增库工程 **`ZeroBot.Synthesize.Abstraction`** 承载 `IVoiceBroadcaster` 及后续共用类型，由 Synthesize（实现方）与 Bilibili / Weibo（消费方）共同引用，**不改动 `ZeroBot.Abstraction`**（评审打回结论）。
- 消费方以 `IEnumerable<IVoiceBroadcaster>` **可选注入**，容器未合并时静默降级，文字通知零影响。
- 无新增插件工程、无新增 NuGet 依赖、`Program.cs` 不变。

## 2. 前置确认（已核实）

- 需求总分支 `feat/notify-voice-broadcast` 基于 main（a096160），当前 HEAD `31fc507`，与 `origin/feat/notify-voice-broadcast` 同步，工作区干净；PRD 与技术方案已在 `docs/plans/` 下。
- 5 处通知发送点现状与技术方案 1.1 表格一致（已逐行读码确认）：

  | 通知 | 发送点 | 现状 |
  |---|---|---|
  | 开播/下播 | `Live/LiveStatusSubscriber.cs:58-70` | `initialized` 分支内 `await foreach (accountId) { foreach (targetGroup) ... }`，`streaming` 时 at 全体 |
  | B 站动态 | `Dynamic/DynamicSubscriber.cs:141-144` | `DynamicMessageBuilder.Build(item.Data, mid)` 后按 `targetGroups` 群发 |
  | 直播间 SC | `Live/LiveScSubscriber.cs:78-87` | `ForwardSuperChatAsync` 内 `SuperChatMessageBuilder.Build(sc)` 后按 `groups` 群发 |
  | 主播弹幕 | `Live/AnchorEventSubscriber.cs:128-143` | `ForwardDanmakuAsync` 内按 `subscription.GroupIds` 群发，仅 `msg.UserId.ToString() == mid` |
  | 微博动态 | `ZeroBot.Weibo/Weibo/WeiboSubscriber.cs:41-45` | `WeiboMessageBuilder.Build(item)` 后按 `targetGroups` 群发 |

- `IBotContext.WriteManyGroupMessageAsync(long accountId, HashSet<long> groupIds, CancellationToken, params OutgoingSegment[])` —— 目标群类型为 `HashSet<long>`，`HashSet<long>` 可直接作为 `IReadOnlyCollection<long>` 传入 `BroadcastAsync`。
- `SynthesizeApi.SynthesizeAsync(endpoint, datasetId, text, lang, ct)` 返回 `byte[]?`，失败/空/异常一律 `null`（`src/plugins/ZeroBot.Synthesize/SynthesizeApi.cs:14`）。
- 语音消息段范式见 `SynthesizeCommandHandler.cs:76-78`：`new RecordOutgoingSegment(new RecordOutgoingSegmentData(new MilkyUri($"base64://{Convert.ToBase64String(bytes)}")))`（`Milky.Net.Model`）。
- 配置读写范式见 `DatasetAliasCommandHandler.cs:64-70`：`config.BeginConfigMutationScopeAsync(async (value, token) => { ...; await config.SaveAsync(value, token); ... })`；`IJsonConfig<T>` 另有 `Current` / `WaitForInitializedAsync`。
- 权限范式：`permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "perm.name", ct)`（`ZeroBot.Utility/ChatPermissionExtensions.cs:32`）。
- `ToTextCommands()`（`ZeroBot.Utility/EventExtensions.cs:12`）以 `/` 为指令前缀、`:：-` 为参数分隔符；`/动态语音播报:启用:小松绿` → `Name = "动态语音播报"`、`Arguments = ["启用", "小松绿"]`。
- 纯文本提取素材已确认：`DynamicMessageBuilder.RenderRichText`（private，可被同类新方法直接调用）、`WeiboMessageBuilder.HtmlToPlainText`（private，同上）；DTO 字段见 `Dynamic/VtuberSpaceApi.cs:53-145`、`Weibo/WeiboApi.cs:51-92`、`Live/LiveScApi.cs:72-95`。
- 测试工程 `test/ZeroBot.Core.Test` 通过 `ZeroBot.Core` 传递引用到各插件工程（`SynthesizeQuotaTest` 已用 `using ZeroBot.Synthesize;` 验证），可直接测试 `ZeroBot.Bilibili` / `ZeroBot.Weibo` 的静态 Builder。

## 3. 实施步骤

按依赖顺序执行，每步完成后 `dotnet build ZeroBot.slnx` 通过再进入下一步。

### Step 1：新增库工程 `src/plugins/ZeroBot.Synthesize.Abstraction/`（新增）

`ZeroBot.Synthesize.Abstraction.csproj`（无工程/包引用，仅 BCL 类型，对齐 `ZeroBot.Endfield.Api` 库工程风格）：

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

</Project>
```

`IVoiceBroadcaster.cs`：

```csharp
namespace ZeroBot.Synthesize.Abstraction;

/// <summary>
/// 通知语音播报契约。由语音合成插件（ZeroBot.Synthesize）实现，通知插件可选消费。
/// 消费方以 <c>IEnumerable&lt;IVoiceBroadcaster&gt;</c> 注入，未注册时解析为空集合即静默不播报。
/// 实现必须自行消化全部异常，不得影响调用方的通知主流程。
/// </summary>
public interface IVoiceBroadcaster
{
    /// <summary>
    /// 对开启了语音播报的群，将 <paramref name="text"/> 合成语音并作为独立消息发送；
    /// <paramref name="groupIds"/> 中未开启播报的群由实现自动过滤。
    /// </summary>
    ValueTask BroadcastAsync(IReadOnlyCollection<long> groupIds, string text,
        CancellationToken cancellationToken = default);
}
```

- 该工程**不是插件**（不实现 `IPlugin`），`Program.cs` 无需改动。
- `ZeroBot.slnx` 的 `/src/plugins/` 分组按字母序附近加入：

  ```xml
  <Project Path="src/plugins/ZeroBot.Synthesize.Abstraction/ZeroBot.Synthesize.Abstraction.csproj" />
  ```

### Step 2：配置扩展 —— `src/plugins/ZeroBot.Synthesize/SynthesizeOptions.cs`（改）

在 `SynthesizeOptions` 内追加两个 init 属性（缺省值保证旧 `synthesize-config.json` 向后兼容，无需迁移）：

```csharp
/// <summary>群 PeerId → 播报音色别名；由 /动态语音播报 指令维护。</summary>
public Dictionary<long, string> VoiceBroadcastGroups { get; init; } = [];

/// <summary>播报文本最大长度（超出截断），默认 200；&lt;= 0 表示不截断。</summary>
public int VoiceBroadcastMaxTextLength { get; init; } = 200;
```

### Step 3：播报服务 —— `src/plugins/ZeroBot.Synthesize/VoiceBroadcastService.cs`（新增）

实现 `IVoiceBroadcaster`，构造注入 `IJsonConfig<SynthesizeOptions> config`、`SynthesizeApi api`、`IBotContext bot`、`ILogger<VoiceBroadcastService> logger`（`AddSingleton<IVoiceBroadcaster, VoiceBroadcastService>()` 注册）。

`BroadcastAsync(groupIds, text, ct)` 逻辑（**整体包在 try/catch 内，任何异常仅 `LogError`，绝不向上抛出**）：

1. `await config.WaitForInitializedAsync(ct)`；取 `var options = config.Current`。
2. 目标群 = `groupIds` ∩ `options.VoiceBroadcastGroups.Keys`（`ToHashSet()`）；为空直接返回。
3. 文本预处理：`var prepared = PrepareText(text, options.VoiceBroadcastMaxTextLength)`；返回 `null` 即返回（空文本不播报）。
4. 按音色别名分组：`targets.GroupBy(g => options.VoiceBroadcastGroups[g])`，每组内 `options.DatasetAliases.TryGetValue(alias, out datasetId)`；
   - 解析不到 → `logger.LogWarning("Voice broadcast alias {Alias} is not bound, skip", alias)` 并 `continue`（对应 PRD「别名被移除」场景）。
   - 每个 `datasetId` **只合成一次**，组内全部群共享同一音频（对应 PRD「同一文本仅合成一次，分发到各群」）。
5. `var bytes = await api.SynthesizeAsync(options.Endpoint, datasetId, prepared, options.Lang, ct)`；`bytes is null` → `LogWarning` 并 `continue`。
6. 构造语音段并分发：

   ```csharp
   var record = new RecordOutgoingSegment(
       new RecordOutgoingSegmentData(new MilkyUri($"base64://{Convert.ToBase64String(bytes)}")));
   await foreach (var (accountId, _) in bot.GetAccountInfoAsync(ct))
   {
       await bot.WriteManyGroupMessageAsync(accountId, groups, ct, [record]);
   }
   ```

   `groups` 为该分组的 `HashSet<long>`；账号遍历与 `SynthesizeCommandHandler` / 各 Subscriber 现有模式一致（多账号各自发送）。

静态纯函数（便于单测）：

```csharp
/// <summary>播报文本预处理：Trim 后为空返回 null（跳过播报）；超过 maxLength 截断（maxLength &lt;= 0 不截断）。</summary>
public static string? PrepareText(string? text, int maxLength)
```

### Step 4：播报指令 —— `src/plugins/ZeroBot.Synthesize/VoiceBroadcastCommandHandler.cs`（新增）

`CommandHandler`，构造注入 `ICommandDispatcher / IPermission / IBotContext / IJsonConfig<SynthesizeOptions>`，结构对齐 `DatasetAliasCommandHandler`。

- 谓词：`message.Scene == MessageScene.Group` 且 `message.ToText().Trim().StartsWith("/动态语音播报")`，且 `await permission.IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "synthesize.voice-broadcast", ct)`。
  - 私聊静默不响应，与 `/学` 一致（PRD「仅群聊可用」）。
  - 前缀与 `/synthesize`、`/学` 互斥，不抢占分发。
- `HandleAsync`：`var command = message.ToTextCommands().FirstOrDefault()`，取 `command.Arguments`：

  | Arguments | 行为 |
  |---|---|
  | `["启用", alias]`（alias 非空白） | `config.Current.DatasetAliases.TryGetValue(alias, out datasetId)` 且 datasetId 非空白才写配置：`BeginConfigMutationScopeAsync` 内 `value.VoiceBroadcastGroups[peerId] = alias` → `SaveAsync` → 回复「已开启本群通知语音播报，音色：{alias}」；否则回复「别名 {alias} 未绑定数据集，请先使用 /synthesize:dataset 绑定」，**不写配置**。重复开启 = 覆盖（换音色），幂等 |
  | `["启用"]`（缺 alias） | 回复 Help |
  | `["禁用", ...]`（多余参数忽略） | `BeginConfigMutationScopeAsync` 内 `value.VoiceBroadcastGroups.Remove(peerId)` → `SaveAsync` → 回复「已关闭本群通知语音播报」；未开启过也回复确认，幂等不报错 |
  | 其他 | 回复 Help |

- Help 文本：

  ```
  /动态语音播报:启用:音色别名
  /动态语音播报:禁用
  ```

- `peerId` 取 `message.Data.PeerId`。

### Step 5：Synthesize 插件注册 —— `src/plugins/ZeroBot.Synthesize/SynthesizePlugin.cs` + `ZeroBot.Synthesize.csproj`（改）

- `ZeroBot.Synthesize.csproj` 的 `<ItemGroup>` 增加：

  ```xml
  <ProjectReference Include="..\ZeroBot.Synthesize.Abstraction\ZeroBot.Synthesize.Abstraction.csproj" />
  ```

- `SynthesizePlugin.BuildComponents` 在现有注册后追加：

  ```csharp
  services.AddSingleton<IVoiceBroadcaster, VoiceBroadcastService>();
  services.AddSingletonComponent<VoiceBroadcastCommandHandler>();
  ```

  并加 `using ZeroBot.Synthesize.Abstraction;`。现有 `SynthesizeApi` / `HttpClient` / `ConfigureJsonConfig` 注册保持不变。

### Step 6：B 站动态纯文本 —— `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicMessageBuilder.cs`（改）

新增 public 静态方法（同文件内可直接调用 private 的 `RenderRichText`，无需提升可见性）：

```csharp
/// <summary>提取动态纯文本（供语音播报）：标题 + 富文本正文，转发链递归拼接，剔除图片、表情占位与链接。</summary>
public static string BuildVoiceText(DynamicData data)
```

- 内部递归私有方法 `AppendVoiceText(DynamicData data, StringBuilder target)`，每层用**局部** `StringBuilder` 收集本层文本（避免父层已有内容影响「本层无文本则回退 desc」的判断），拼完再追加到 `target`：
  1. `data.Type == LiveRcmdType`：解析 `LiveRcmdContent` 得 `LivePlayInfo`，返回 `"[正在直播] {Title}"`（**不拼链接、不拼封面**）；解析失败或无标题返回空串。
  2. `data.Type == ForwardType` 且 `moduleDynamic.Desc != null`：追加 `RenderRichText(moduleDynamic.Desc)`（转发人自己的话）。
  3. `opus != null`：`opus.Title` 非空白则追加 `Title.Trim()` + 换行，再追加 `RenderRichText(opus.Summary)`；**不追加 `JumpUrl` / 原动态链接 / `Pics`**。
  4. 本层仍为空且 `moduleDynamic.Desc != null`：回退 `RenderRichText(moduleDynamic.Desc)`（对齐 `AppendDynamic` 的 fallback）。
  5. `data.Type == ForwardType && data.Orig != null`：追加换行后递归 `AppendVoiceText(data.Orig, target)`。
- **不**追加 `[{data.Type}] 暂无可用文本内容` 占位（那是展示文案，不适合朗读）。
- `BuildVoiceText` 返回 `Trim()` 后结果；无文本返回 `""`（调用方跳过）。

### Step 7：微博纯文本 —— `src/plugins/ZeroBot.Weibo/Weibo/WeiboMessageBuilder.cs`（改）

新增 public 静态方法（同文件内直接调用 private 的 `HtmlToPlainText`）：

```csharp
/// <summary>提取微博纯文本（供语音播报）：转发含转发人文本与「@原作者: 正文」，剔除图片与链接。</summary>
public static string BuildVoiceText(WeiboTimelineItem item)
```

- `item.Data == null` → 返回 `""`。
- 转发微博（`data.RetweetedStatus != null`）：
  - `forwardText = HtmlToPlainText(data.Text ?? "")`，按 `AppendWeibo` 同款逻辑截掉 `//@...` 尾巴（`LastIndexOf("//@")`，`> 0` 时截断）后 `Trim()`；
  - `origText = HtmlToPlainText(data.RetweetedStatus.Text ?? "")`；`RetweetedStatus.User == null` 或 `origText` 为空或含「微博已被删除」时**只返回转发人文本**（跳过 `[原微博不可见]` 占位）；
  - 否则拼接为 `{forwardText}\n@{User.ScreenName}: {origText}`。
- 普通微博：返回 `HtmlToPlainText(data.Text ?? "")`。
- 一律**不含**原文链接（`https://m.weibo.cn/status/{mblogId}`）与 `Pics`；结果 `Trim()`，空则 `""`。

### Step 8：消费方接入 —— 4 个 Bilibili Subscriber + `ZeroBot.Bilibili.csproj`（改）

`ZeroBot.Bilibili.csproj` 增加 `<ProjectReference Include="..\ZeroBot.Synthesize.Abstraction\ZeroBot.Synthesize.Abstraction.csproj" />`。

4 个 Subscriber（`LiveStatusSubscriber` / `DynamicSubscriber` / `LiveScSubscriber` / `AnchorEventSubscriber`）构造函数**末尾**追加可选注入 `IEnumerable<IVoiceBroadcaster> broadcasters`（`using ZeroBot.Synthesize.Abstraction;`；无实现时解析为空集合，构造不受影响），并各加一个同名私有方法：

```csharp
private async Task BroadcastVoiceAsync(IReadOnlyCollection<long> groupIds, string? text, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(text)) return;
    foreach (var broadcaster in broadcasters)
    {
        try
        {
            await broadcaster.BroadcastAsync(groupIds, text, ct);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Voice broadcast failed");
        }
    }
}
```

挂载点（**一律在文字通知发送之后**调用）：

| 文件 | 位置 | 调用 | 播报文本 | 目标群 |
|---|---|---|---|---|
| `Live/LiveStatusSubscriber.cs` | `initialized` 分支内，`await foreach (accountId)` 发送循环**之后**，且仅 `streaming` 为 true（下播不播报） | `await BroadcastVoiceAsync(targetGroups, $"噔噔咚，开始直播了哦。今天播「{info.Title}」。", cancellationToken);` | 开播模板 | `targetGroups` |
| `Dynamic/DynamicSubscriber.cs` | `await foreach (accountId)` 发送循环之后 | `await BroadcastVoiceAsync(targetGroups, DynamicMessageBuilder.BuildVoiceText(item.Data), cancellationToken);` | 动态纯文本 | `targetGroups` |
| `Live/LiveScSubscriber.cs` | `ForwardSuperChatAsync` 的 `await foreach (accountId)` 发送循环之后 | 见下方内联文本 | SC 模板 | `groups` |
| `Live/AnchorEventSubscriber.cs` | `ForwardDanmakuAsync` 内每个匹配订阅项的 `await foreach (accountId)` 发送循环之后 | `await BroadcastVoiceAsync(subscription.GroupIds, msg.Msg, cancellationToken);`（`msg.Msg` 空白时由 `BroadcastVoiceAsync` 前置判断跳过，覆盖表情包弹幕） | 弹幕文字 | `subscription.GroupIds` |

`LiveScSubscriber` 的内联文本（name 判空逻辑与 `SuperChatMessageBuilder.Build` 一致）：

```csharp
var name = sc.UserInfo?.Uname;
if (string.IsNullOrWhiteSpace(name)) name = "未知用户";
await BroadcastVoiceAsync(groups, $"感谢{name}发送的{sc.Price}元醒目留言，{sc.Message}", cancellationToken);
```

- `AnchorEventSubscriber.ForwardInteractAsync`（入场事件）**不接**（PRD 非目标）。
- `DynamicSubscriber` 已在发送前 `continue` 掉 `DYNAMIC_TYPE_LIVE_RCMD`，`BuildVoiceText` 的 LiveRcmd 分支为防御性实现。

### Step 9：消费方接入 —— `ZeroBot.Weibo/Weibo/WeiboSubscriber.cs` + `ZeroBot.Weibo.csproj`（改）

- `ZeroBot.Weibo.csproj` 增加 `<ProjectReference Include="..\ZeroBot.Synthesize.Abstraction\ZeroBot.Synthesize.Abstraction.csproj" />`。
- `WeiboSubscriber` 构造函数末尾追加 `IEnumerable<IVoiceBroadcaster> broadcasters`，加与 Step 8 相同的私有 `BroadcastVoiceAsync`。
- 在 `if (!string.IsNullOrEmpty(lastMblogId))` 分支的 `await foreach (accountId)` 发送循环**之后**：

  ```csharp
  await BroadcastVoiceAsync(targetGroups, WeiboMessageBuilder.BuildVoiceText(item), cancellationToken);
  ```

### Step 10：构建接入 —— `src/ZeroBot.Core/Dockerfile`（改）

在既有插件 csproj COPY 段补一行（供容器构建 restore，与既有条目同模式）：

```dockerfile
COPY ["src/plugins/ZeroBot.Synthesize.Abstraction/ZeroBot.Synthesize.Abstraction.csproj", "src/plugins/ZeroBot.Synthesize.Abstraction/"]
```

`Program.cs` 不变（库工程非插件；`SynthesizePlugin` 已注册，其内新增服务注册即可）。

### Step 11：文档 —— `src/plugins/AGENTS.md`（改）

- 「现有插件列表」新增 `ZeroBot.Synthesize.Abstraction` 条目：路径 `src/plugins/ZeroBot.Synthesize.Abstraction/`、命名空间 `ZeroBot.Synthesize.Abstraction`、功能（语音播报等 Synthesize 域共用契约库，非插件，被 Synthesize / Bilibili / Weibo 共同引用）、核心类型 `IVoiceBroadcaster`。
- `ZeroBot.Synthesize` 条目补充：
  - 组件：`VoiceBroadcastService`（通知语音播报实现）、`VoiceBroadcastCommandHandler`（播报开关指令）；
  - 命令：`/动态语音播报:启用:{alias}` / `/动态语音播报:禁用`（仅群聊，权限 `synthesize.voice-broadcast`）；
  - 配置文件：`synthesize-config.json` 补充 `voiceBroadcastGroups` / `voiceBroadcastMaxTextLength` 字段说明。

### Step 12：单元测试 —— `test/ZeroBot.Core.Test/`（新增/改）

纯函数覆盖（参照 `SynthesizeQuotaTest` 风格，xunit，`test/ZeroBot.Core.Test/` 下新增文件）：

- `VoiceBroadcastServiceTest.cs`：
  - `PrepareText`：空白/空串返回 `null`；`Trim` 生效；超长按 `maxLength` 截断；`maxLength <= 0` 不截断；恰好等于长度不截断。
- `DynamicMessageBuilderVoiceTextTest.cs`（`DynamicMessageBuilder.BuildVoiceText`）：
  - 普通动态（opus 标题 + 正文）→ 「标题\n正文」，无 URL、无图片占位；
  - 无标题动态 → 仅正文；
  - 纯图片动态 → 空串；
  - 转发动态 → 转发人文本 + 被转文本递归拼接；
  - 无 opus 时回退 `desc`。
- `WeiboMessageBuilderVoiceTextTest.cs`（`WeiboMessageBuilder.BuildVoiceText`）：
  - 普通微博 HTML → 纯文本（`<br>` / `<a>` / `<img>` 剔除、实体转义还原）；
  - 转发微博 → 转发人文本 + 「@原作者: 正文」，`//@` 尾巴截断；
  - 原微博不可见/已删除 → 只含转发人文本；
  - `data == null` → 空串；纯图片微博 → 空串。

## 4. 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/plugins/ZeroBot.Synthesize.Abstraction/ZeroBot.Synthesize.Abstraction.csproj` | 新增（库工程，无依赖） |
| `src/plugins/ZeroBot.Synthesize.Abstraction/IVoiceBroadcaster.cs` | 新增（播报契约及后续共用类型） |
| `src/plugins/ZeroBot.Synthesize/ZeroBot.Synthesize.csproj` | 改：引用 Synthesize.Abstraction |
| `src/plugins/ZeroBot.Synthesize/SynthesizeOptions.cs` | 改：加 `VoiceBroadcastGroups` / `VoiceBroadcastMaxTextLength` |
| `src/plugins/ZeroBot.Synthesize/VoiceBroadcastService.cs` | 新增（`IVoiceBroadcaster` 实现） |
| `src/plugins/ZeroBot.Synthesize/VoiceBroadcastCommandHandler.cs` | 新增（启用/禁用指令） |
| `src/plugins/ZeroBot.Synthesize/SynthesizePlugin.cs` | 改：注册 `IVoiceBroadcaster` 与指令组件 |
| `src/plugins/ZeroBot.Bilibili/ZeroBot.Bilibili.csproj` | 改：引用 Synthesize.Abstraction |
| `src/plugins/ZeroBot.Bilibili/Live/LiveStatusSubscriber.cs` | 改：开播播报挂载 |
| `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicSubscriber.cs` | 改：动态播报挂载 |
| `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicMessageBuilder.cs` | 改：加 `BuildVoiceText` |
| `src/plugins/ZeroBot.Bilibili/Live/LiveScSubscriber.cs` | 改：SC 播报挂载 |
| `src/plugins/ZeroBot.Bilibili/Live/AnchorEventSubscriber.cs` | 改：主播弹幕播报挂载 |
| `src/plugins/ZeroBot.Weibo/ZeroBot.Weibo.csproj` | 改：引用 Synthesize.Abstraction |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboSubscriber.cs` | 改：微博播报挂载 |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboMessageBuilder.cs` | 改：加 `BuildVoiceText` |
| `ZeroBot.slnx` | 改：`/src/plugins/` 分组加入新库工程 |
| `src/ZeroBot.Core/Dockerfile` | 改：新增新库工程 csproj 的 COPY |
| `src/plugins/AGENTS.md` | 改：新增 Synthesize.Abstraction 条目、Synthesize 条目补充 |
| `test/ZeroBot.Core.Test/VoiceBroadcastServiceTest.cs` | 新增 |
| `test/ZeroBot.Core.Test/DynamicMessageBuilderVoiceTextTest.cs` | 新增 |
| `test/ZeroBot.Core.Test/WeiboMessageBuilderVoiceTextTest.cs` | 新增 |
| `docs/plans/notify-voice-broadcast-impl.md` | 新增（本文档） |

**不改 `ZeroBot.Abstraction`**；无新增插件工程、无新增 NuGet 依赖、无 `Directory.Packages.props` 改动；`Program.cs` 不变；**不涉及任何线上配置文件**（`synthesize-config.json` 由插件自身热加载维护，旧文件缺省字段自动回落默认值）。

## 5. 验证

1. 构建：`dotnet build ZeroBot.slnx -c Release` 编译通过、无新增告警。
2. 测试：`dotnet test test/ZeroBot.Core.Test/ZeroBot.Core.Test.csproj -c Release` 全绿（含本次新增纯函数用例 + 既有回归）。
3. 部署验证（按仓库部署手册：`dotnet publish src/ZeroBot.Core/ZeroBot.Core.csproj -c Release` → `pm2 restart ZeroBot --update-env`）后，对照 PRD 第 7 节 11 条验收标准执行，重点：
   - `/动态语音播报:启用:已绑定别名` → 确认回复 + `synthesize-config.json` 出现该群配置；`启用:未绑定别名` → 提示且不写配置；
   - 已订阅开播/动态/微博/SC/主播弹幕的群 → 文字通知后追加一条独立语音，音色正确、文本符合模板；下播与主播入场无语音；
   - 未开启播报的群、开启播报但未订阅任何通知的群 → 无语音；
   - `/动态语音播报:禁用` 后无语音，重复禁用不报错；
   - 模拟合成接口不可用 → 文字通知正常、无语音、日志有记录；
   - 重启 Bot 后播报配置自动恢复；
   - **容器合并语义验证**：若开启播报后完全无语音且日志无 `VoiceBroadcastService` 记录，说明 EmberFramework 未合并插件容器（技术方案 5 节风险项），此时把 5 处 `IEnumerable<IVoiceBroadcaster>` 改为直接注入 `IVoiceBroadcaster` 即可（编译期引用已具备，改动极小）。

## 6. 备注

- **`BroadcastVoiceAsync` 私有方法在 5 个消费方各有一份**（约 8 行，含 try/catch + 日志）：按技术方案设计保留为各消费方私有方法，避免为提取公共扩展而在 `ZeroBot.Synthesize.Abstraction` 引入 `Microsoft.Extensions.Logging.Abstractions` 依赖（该库约定「无包引用，仅 BCL 类型」）。若后续消费方继续增多，再评估把「可选注入 + 失败隔离」下沉为库内扩展方法。
- **SC 播报文本内联在 `LiveScSubscriber`**（技术方案指定位置），发送人判空逻辑与 `SuperChatMessageBuilder` 有 3 行重复；为保持技术方案文件变更清单不变而内联，若 SC 文案再变化则提取为 `SuperChatMessageBuilder.BuildVoiceText`。
- **参数分隔符 `-`**：`ToTextCommands()` 默认以 `:：-` 切分参数，别名含 `-` 时会被切开（与 `/synthesize:dataset` 等既有指令行为一致）；本需求别名为中文音色名，不受影响。
- **语音播报不消耗 `/学` 每日额度**（技术方案决策点 5）：`VoiceBroadcastService` 不触碰 `DailyQuotas`，仅以 `VoiceBroadcastMaxTextLength`（默认 200 字）截断控制单次合成开销。
- **失败完全隔离**：`VoiceBroadcastService.BroadcastAsync` 整体 try/catch + 消费方 `BroadcastVoiceAsync` 逐 broadcaster try/catch 双重兜底，合成/发送任何失败仅记日志，文字通知主流程零影响、不回复、不重试。
- **语音在文字通知之后发送**：所有挂载点均在文字 `WriteManyGroupMessageAsync` 循环结束后调用；同一通知多群共享同一段音频（按 datasetId 分组只合成一次）。
