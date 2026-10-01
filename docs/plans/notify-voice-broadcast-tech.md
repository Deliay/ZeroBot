# 通知语音播报 —— 技术方案

| 属性 | 值 |
|---|---|
| 所属插件 | ZeroBot.Synthesize.Abstraction（接口与共用类型库，新增）、ZeroBot.Synthesize（播报实现与指令）、ZeroBot.Bilibili / ZeroBot.Weibo（触发点接入） |
| 关联文档 | [产品需求文档（PRD）](./notify-voice-broadcast-prd.md) |
| 参照实现 | [语音合成插件](./synthesize-voice-tech.md)（`SynthesizeApi` / 别名配置 / `RecordOutgoingSegment` 发送）；Endfield 家族库工程模式（`ZeroBot.Endfield.Api`） |

---

## 1. 现状分析（已读码确认）

### 1.1 通知发送链路（播报挂载点，共 5 处）

所有通知的共同模式：注入 `IBotContext bot` → `await foreach (var (accountId, _) in bot.GetAccountInfoAsync(ct))` → `bot.WriteManyGroupMessageAsync(accountId, groups, ct, segments)`，消息为 `OutgoingSegment[]`。

| 通知 | 发送点 | 订阅配置（群开启状态的判定依据） |
|---|---|---|
| 开播/下播 | `Live/LiveStatusSubscriber.cs:63`（`streaming == true` 且 `initialized` 分支为开播） | `BilibiliOptions.RoomIdToGroupSubscriptions` |
| B 站动态 | `Dynamic/DynamicSubscriber.cs:45-48`，`DynamicMessageBuilder.Build(item.Data, mid)` | `BilibiliOptions.MidToGroupSubscriptions` |
| 直播间 SC | `Live/LiveScSubscriber.cs:78-87`（`ForwardSuperChatAsync`），`SuperChatMessageBuilder.Build(sc)` | `BilibiliOptions.ScRoomIdToGroupSubscriptions` |
| 主播弹幕/入场 | `Live/AnchorEventSubscriber.cs:128-143`（`ForwardDanmakuAsync`，仅订阅 mid 本人弹幕） | `BilibiliOptions.AnchorEventSubscriptions` |
| 微博动态 | `ZeroBot.Weibo/Weibo/WeiboSubscriber.cs:41-45`，`WeiboMessageBuilder.Build(item)` | `WeiboOptions.UidToGroupSubscriptions` |

「通知已在群内开启」= 群 ID 出现在上述订阅字典的 `HashSet<long>` 中，发送路径无其他群级开关；播报挂在这些发送点之后即天然满足 PRD 的前置条件。

### 1.2 语音合成能力（ZeroBot.Synthesize）

- `SynthesizeApi`（`src/plugins/ZeroBot.Synthesize/SynthesizeApi.cs:9`，`public`，单例）：
  ```csharp
  public async Task<byte[]?> SynthesizeAsync(string endpoint, string datasetId, string text, string lang,
      CancellationToken cancellationToken = default)
  ```
  POST `{endpoint}/api/training/datasets/{datasetId}/synthesize`，返回音频二进制；失败/空响应/异常均返回 `null`，异常不抛出。
- 别名：`SynthesizeOptions.DatasetAliases`（`Dictionary<string alias, string datasetId>`），`synthesize-config.json` 热加载；`Endpoint` / `Lang` 同配置。
- 语音发送范例（`SynthesizeCommandHandler.cs:76-78`）：
  ```csharp
  var record = new RecordOutgoingSegment(
      new RecordOutgoingSegmentData(new MilkyUri($"base64://{Convert.ToBase64String(bytes)}")));
  ```
- 插件注册（`SynthesizePlugin.cs`）：`ConfigureJsonConfig("synthesize-config.json", ...)`、`AddSingleton<HttpClient>()`、`AddSingleton<SynthesizeApi>()`，全部单例。

### 1.3 跨插件调用约束

- 各业务插件工程仅引用 `ZeroBot.Abstraction` + `ZeroBot.Utility`，**无业务插件互相引用的先例**；跨插件共用的契约应放入独立库工程，先例为 Endfield 家族：`ZeroBot.Endfield` 引用同目录库工程 `ZeroBot.Endfield.Api`（不实现 `IPlugin`）。
- 因此本方案新增库工程 **`ZeroBot.Synthesize.Abstraction`** 承载播报接口与共用类型，由 `ZeroBot.Synthesize`（实现方）与 `ZeroBot.Bilibili` / `ZeroBot.Weibo`（消费方）共同引用，**不改动 `ZeroBot.Abstraction`**。
- EmberFramework（外部包）是否把各插件 `BuildComponents` 返回的 `IServiceCollection` 合并为单一容器**未在本仓库验证**。因此跨插件注入按「可选依赖」设计（`IEnumerable<IVoiceBroadcaster>`），缺失时静默降级，不导致宿主插件构造失败。

### 1.4 动态/微博纯文本提取

- `DynamicMessageBuilder`（静态类）：文本由 `RenderRichText(moduleDynamic.Desc)` 与 `opus.Title` + `RenderRichText(opus.Summary)` 拼接，图片在 `opus.Pics` 单独成段，URL 单独拼接——提取纯文本只需复用 `RenderRichText` 与 desc/opus 部分，剔除 URL 与 pics。
- `WeiboMessageBuilder`：`HtmlToPlainText(data.Text ?? "")`（private）已完成 HTML→纯文本与表情/标签剔除，转发展开含 `---- 转发 ----` 与 `@{author}: {text}`；提取纯文本复用该方法即可。

## 2. 方案设计

整体思路：**新增 `ZeroBot.Synthesize.Abstraction` 库工程定义播报接口，Synthesize 插件实现，Bilibili/Weibo 引用该库并在通知发送点后以 `IEnumerable<IVoiceBroadcaster>` 可选注入调用**。无新增插件工程、无新增 NuGet 依赖、不改 `ZeroBot.Abstraction`。

### 2.1 新增库工程 ZeroBot.Synthesize.Abstraction

```
src/plugins/ZeroBot.Synthesize.Abstraction/
├── ZeroBot.Synthesize.Abstraction.csproj
└── IVoiceBroadcaster.cs
```

`ZeroBot.Synthesize.Abstraction.csproj`（无工程/包引用，仅 BCL 类型，对齐 Endfield.Api 库工程风格）：

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

/// <summary>通知语音播报。由语音合成插件（ZeroBot.Synthesize）实现，通知插件可选消费；未注册时注入空集合即静默不播报。</summary>
public interface IVoiceBroadcaster
{
    /// <summary>对开启了语音播报的群，将 text 合成语音并作为独立消息发送；groupIds 中未开启的群自动过滤。</summary>
    ValueTask BroadcastAsync(IReadOnlyCollection<long> groupIds, string text, CancellationToken cancellationToken = default);
}
```

- 当前共用类型仅 `IVoiceBroadcaster`；后续若消费方需要更多播报契约（如播报状态查询），同样放本工程。
- 该库**不是插件**（不实现 `IPlugin`），`Program.cs` 无需注册改动。

### 2.2 Synthesize 插件：播报服务与指令

`ZeroBot.Synthesize.csproj` 增加 `ProjectReference` 指向 `ZeroBot.Synthesize.Abstraction`。

#### 配置扩展（`SynthesizeOptions.cs`）

```csharp
/// <summary>群 PeerId → 播报音色别名</summary>
public Dictionary<long, string> VoiceBroadcastGroups { get; init; } = [];

/// <summary>播报文本最大长度（超出截断），默认 200</summary>
public int VoiceBroadcastMaxTextLength { get; init; } = 200;
```

向后兼容旧 `synthesize-config.json`（缺省为空字典 / 默认值）。

#### `VoiceBroadcastService.cs`（实现 `IVoiceBroadcaster`）

注入 `IJsonConfig<SynthesizeOptions>`、`SynthesizeApi`、`IBotContext`、`ILogger<VoiceBroadcastService>`。`BroadcastAsync`：

1. `config.WaitForInitializedAsync` 后，用 `groupIds` 交集 `config.Current.VoiceBroadcastGroups.Keys` 得到目标群；为空直接返回。
2. 取该群别名 → `DatasetAliases.TryGetValue(alias, out datasetId)`；不同群配置了不同别名时按 datasetId 分组，每个 datasetId 合成一次；别名解析不到 → 告警日志并跳过该组。
3. 文本预处理：`Trim()`；为空跳过；超过 `VoiceBroadcastMaxTextLength` 截断。
4. `api.SynthesizeAsync(options.Endpoint, datasetId, text, options.Lang, ct)`；返回 `null` → 告警日志并跳过。
5. 构造 `RecordOutgoingSegment`（base64，`SynthesizeCommandHandler` 同款），遍历 `bot.GetAccountInfoAsync` → `bot.WriteManyGroupMessageAsync(accountId, targetGroups, ct, [record])`。
6. 整体 try/catch：任何异常仅记日志，绝不向上抛出影响文字通知。

注册：`services.AddSingleton<IVoiceBroadcaster, VoiceBroadcastService>();`（同时保留现有 `SynthesizeApi` / `HttpClient` 注册）。

#### `VoiceBroadcastCommandHandler.cs`（`CommandHandler`）

- 谓词：群聊且 `text.StartsWith("/动态语音播报")`，权限 `IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "synthesize.voice-broadcast")`。
- `启用:{alias}`：`config.Current.DatasetAliases` 含该别名才在 `BeginConfigMutationScopeAsync` 内写 `VoiceBroadcastGroups[peerId] = alias` 并 `SaveAsync`，回复「已开启本群通知语音播报，音色：{alias}」；未绑定回复提示。重复开启 = 覆盖（换音色）。
- `禁用`（忽略后续多余参数）：移除 `VoiceBroadcastGroups[peerId]` 并 `SaveAsync`，回复「已关闭本群通知语音播报」；未开启幂等。
- 参数无法解析时回复 Help：

  ```
  /动态语音播报:启用:音色别名
  /动态语音播报:禁用
  ```

与既有 `DatasetAliasCommandHandler`（`/synthesize` 前缀）、`SynthesizeCommandHandler`（`/学` 前缀）前缀互斥，不会抢占分发。

### 2.3 Bilibili 插件接入（4 个 Subscriber）

`ZeroBot.Bilibili.csproj` 增加 `ProjectReference` 指向 `ZeroBot.Synthesize.Abstraction`。

4 个 Subscriber 构造函数追加可选注入 `IEnumerable<IVoiceBroadcaster> broadcasters`（无实现时为空集合，构造不受影响），并加私有方法：

```csharp
private async Task BroadcastVoiceAsync(IReadOnlyCollection<long> groupIds, string text, CancellationToken ct)
{
    foreach (var broadcaster in broadcasters)
    {
        try { await broadcaster.BroadcastAsync(groupIds, text, ct); }
        catch (Exception e) { logger.LogWarning(e, "Voice broadcast failed"); }
    }
}
```

各挂载点在**文字通知发送之后**调用（失败已隔离，不影响通知主流程）：

| 文件 | 位置 | 播报文本 |
|---|---|---|
| `Live/LiveStatusSubscriber.cs` | `:63` 发送循环后，仅 `streaming && initialized` 分支 | `$"噔噔咚，开始直播了哦。今天播「{info.Title}」。"`，目标群 `targetGroups` |
| `Dynamic/DynamicSubscriber.cs` | `:45-48` 发送后 | `DynamicMessageBuilder.BuildVoiceText(item.Data)`，目标群 `targetGroups` |
| `Live/LiveScSubscriber.cs` | `ForwardSuperChatAsync` 发送后 | `$"感谢{name}发送的{sc.Price}元醒目留言，{sc.Message}"`（name 取 `UserInfo?.Uname`，空则「未知用户」，与 `SuperChatMessageBuilder` 一致），目标群 `groups` |
| `Live/AnchorEventSubscriber.cs` | `ForwardDanmakuAsync` 发送后 | `msg.Msg`（`string.IsNullOrWhiteSpace` 时不调用，覆盖表情包弹幕），目标群 `subscription.GroupIds`；入场事件 `ForwardInteractAsync` 不接 |

`DynamicMessageBuilder` 新增（`Dynamic/DynamicMessageBuilder.cs`）：

```csharp
/// <summary>提取动态纯文本（供语音播报）：标题 + 富文本正文，转发链递归，剔除图片与链接。</summary>
public static string BuildVoiceText(DynamicData data)
```

复用现有 `RenderRichText` 与 desc/opus 取值逻辑（含 `DYNAMIC_TYPE_FORWARD` 时转发人文本 + 被转文本递归），不拼接 URL、不处理 pics；`LiveRcmdType` 返回 `[正在直播] {title}`；无文本时返回空串（调用方跳过）。

### 2.4 Weibo 插件接入

`ZeroBot.Weibo.csproj` 增加 `ProjectReference` 指向 `ZeroBot.Synthesize.Abstraction`。

`WeiboSubscriber` 同样可选注入 `IEnumerable<IVoiceBroadcaster>`，在 `:41-45` 发送后调用 `BroadcastVoiceAsync(targetGroups, WeiboMessageBuilder.BuildVoiceText(item), ct)`。

`WeiboMessageBuilder` 新增：

```csharp
/// <summary>提取微博纯文本（供语音播报）：转发含转发人文本与「@原作者: 正文」，剔除图片。</summary>
public static string BuildVoiceText(WeiboTimelineItem item)
```

复用现有 `HtmlToPlainText`（提升可见性或经新 public 方法内部调用），不含原文链接与 pics；`data == null` 返回空串。

### 2.5 工程与文档注册

- `ZeroBot.slnx`：`/src/plugins/` 分组加入 `ZeroBot.Synthesize.Abstraction`。
- `src/ZeroBot.Core/Dockerfile`：新增一行 `COPY ["src/plugins/ZeroBot.Synthesize.Abstraction/ZeroBot.Synthesize.Abstraction.csproj", "src/plugins/ZeroBot.Synthesize.Abstraction/"]`（供容器构建 restore，与既有插件 csproj 同模式）。
- `Program.cs` 无需改动（库工程非插件；`SynthesizePlugin` 已注册，其内新增服务注册即可）。
- `src/plugins/AGENTS.md`：补充 `ZeroBot.Synthesize.Abstraction` 条目，并在 ZeroBot.Synthesize 条目补充 `/动态语音播报` 指令与 `VoiceBroadcastGroups` 配置说明。

## 3. 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/plugins/ZeroBot.Synthesize.Abstraction/ZeroBot.Synthesize.Abstraction.csproj` | 新增（库工程，无依赖） |
| `src/plugins/ZeroBot.Synthesize.Abstraction/IVoiceBroadcaster.cs` | 新增（播报接口及后续共用类型） |
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
| `ZeroBot.slnx` | 改：加入新库工程 |
| `src/ZeroBot.Core/Dockerfile` | 改：新增新库工程 csproj 的 COPY |
| `src/plugins/AGENTS.md` | 改：新增 Synthesize.Abstraction 条目、Synthesize 条目补充 |
| `docs/plans/notify-voice-broadcast-prd.md` | 新增（PRD） |
| `docs/plans/notify-voice-broadcast-tech.md` | 新增（本文档） |

**不改 `ZeroBot.Abstraction`**；无新增插件工程、无新增 NuGet 依赖；`Program.cs` 不变。

## 4. 关键决策点

1. **共用契约放独立库工程 `ZeroBot.Synthesize.Abstraction`**（评审结论）：`IVoiceBroadcaster` 及相关共用类型不侵入 `ZeroBot.Abstraction`，由实现方（Synthesize）与消费方（Bilibili/Weibo）共同引用，沿用 Endfield 家族库工程的既有先例，保持 `ZeroBot.Abstraction` 只承载框架级抽象。
2. **消费方仍以 `IEnumerable<IVoiceBroadcaster>` 可选注入**：EmberFramework 容器合并语义未在仓库内验证，该写法在「容器合并」时正常播报、在「容器隔离」时解析为空集合静默降级，两种情况下宿主插件都能正常构造与运行，文字通知零影响。上线后若发现不播报，首先排查容器合并问题，届时可改为直接注入（编译期引用已具备）。
3. **播报配置归属 synthesize-config.json**：别名与合成 endpoint 均在 Synthesize 插件，播报音色别名天然同域；`Dictionary<long groupId, string alias>` 结构简单，热加载后下一次通知即生效。
4. **同一文本合成一次、多群多账号分发**：同一通知对全部开启播报的群文本相同（按 datasetId 分组），避免重复调用合成接口；账号遍历复用 `IBotContext` 现有模式。
5. **不消耗用户每日额度**：播报为订阅后的自动行为，非用户主动触发，与 `/学` 的防滥用额度目标不同；以 `VoiceBroadcastMaxTextLength` 截断控制单次合成开销。后续如需总量控制可复用 `SynthesizeQuota` 思路另设播报额度。
6. **开启指令不校验群订阅状态**：播报挂载在通知发送点之后，群未开启对应通知时链路不会触达该群，「已开启通知的群才能收到播报」由结构天然保证（PRD 3.4）；避免为校验引入 Bilibili 配置读取的跨插件耦合。
7. **失败完全隔离**：播报所有异常在 `VoiceBroadcastService` 与调用侧双重 try/catch 内消化，仅记日志；合成失败不回复、不重试、不影响文字通知。
8. **表情包弹幕不播报**：主播弹幕挂载点先判 `msg.Msg` 空白即跳过；动态/微博纯文本提取复用既有 Builder 逻辑，剔除图片/表情占位与 URL，保证播报文本可朗读。

## 5. 风险与缓解

| 风险 | 缓解 |
|---|---|
| EmberFramework 未合并插件容器导致播报静默缺失 | 设计上零副作用降级；上线验收标准第 3 条可直接暴露；因已有编译期引用，确认未合并后改为直接注入即可（改动小） |
| 高频通知（如动态连发）产生合成压力 | 触发源均为人为内容（开播/动态/SC/主播弹幕），频率天然低；文本截断限制单次开销；后续可加最小播报间隔 |
| 别名改绑/删除后播报音色漂移或失效 | 解析以播报时配置为准（改绑即换音色）；删除后告警跳过，文字通知不受影响 |
| base64 语音体积大 | 与 `/学` 现有发送路径一致，可接受 |
| `VoiceBroadcastGroups` 写入与其他配置写入并发 | 均经 `BeginConfigMutationScopeAsync` 串行化 mutation，与现有指令写配置模式一致 |
| 新增库工程后旧 Dockerfile/解决方案未同步 | 文件变更清单已包含 slnx 与 Dockerfile 改动，提交前 `dotnet build` 全量验证 |
