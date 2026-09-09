# 实现方案：微博订阅推送（ZeroBot.Weibo 插件）

- 日期：2026-09-09
- 状态：待实现
- 需求总分支：`feat/weibo-subscription`
- 关联文档：[weibo-subscription-prd.md](weibo-subscription-prd.md)（产品需求）、[weibo-subscription-tech.md](weibo-subscription-tech.md)（技术提案）

## 目标与范围

按技术提案新建独立插件 `src/plugins/ZeroBot.Weibo/`，复刻 `src/plugins/ZeroBot.Bilibili/Dynamic/` 四件套模式（CommandHandler / Subscriber / Api / MessageBuilder + Options + Plugin 入口），实现：

- 命令 `/微博:订阅:{uid}`、`/微博:取消:{uid}`；
- 订阅时调用远端 `POST /api/weibo/subscription`，取消仅改本地；
- 20s 轮询 timeline，`mblogId` 游标去重，首次只记录不推送；
- HTML 清洗正文 + 配图 + 转发递归渲染 + 原文链接。

非目标与 PRD 一致：不做远端取消、不展示互动数据、不展开长微博、不做订阅列表查询命令。

## 参照实现

逐文件对照 `src/plugins/ZeroBot.Bilibili/`：

| 参照 | 新文件 |
|---|---|
| `BiliBiliPlugin.cs` | `WeiboPlugin.cs` |
| `BilibiliOptions.cs` | `WeiboOptions.cs` |
| `Dynamic/VtuberSpaceApi.cs` | `Weibo/WeiboApi.cs` |
| `Dynamic/DynamicCommandHandler.cs` | `Weibo/WeiboCommandHandler.cs` |
| `Dynamic/DynamicSubscriber.cs` | `Weibo/WeiboSubscriber.cs` |
| `Dynamic/DynamicMessageBuilder.cs` | `Weibo/WeiboMessageBuilder.cs` |

关键差异（与 B站动态推送不同的点，实现时务必注意）：

1. **取消订阅不调用远端接口**：`DynamicCommandHandler` 在最后一个群取消时调 `api.UnsubscribeAsync`，微博版**不得**保留该调用（PRD 非目标第 1 条）。
2. **游标类型为 string（`mblogId`）**，配置键为 `UidToGroupSubscriptions` / `LastWeiboIds`。
3. **正文是 HTML 片段**，需清洗（B站是结构化富文本节点，无需清洗）。
4. **图片取 `pics[].large.url`**（兜底 `original_pic`），非 `opus.pics[].url`。
5. **无直播推荐等类型过滤**（B站的 `DYNAMIC_TYPE_LIVE_RCMD` 跳过逻辑不适用于微博）。
6. `VtuberServerOptions` 在本插件程序集内重复定义同名 record，不跨程序集引用 Bilibili。

## 实现步骤

### Step 1：新建项目骨架

- 新建 `src/plugins/ZeroBot.Weibo/ZeroBot.Weibo.csproj`，内容按 `src/plugins/AGENTS.md`「创建新插件」模板：net10.0、ImplicitUsings、Nullable，引用 `ZeroBot.Abstraction` + `ZeroBot.Utility`。无第三方包依赖。
- 命名空间 `ZeroBot.Weibo`。

### Step 2：`WeiboOptions.cs`（热加载配置）

```csharp
public record WeiboOptions
{
    public static WeiboOptions Default => new();
    public Dictionary<string, HashSet<long>> UidToGroupSubscriptions { get; init; } = [];
    public Dictionary<string, string> LastWeiboIds { get; init; } = [];
}
```

### Step 3：`Weibo/WeiboApi.cs`（HTTP + DTO）

仿 `VtuberSpaceApi`（注入 `HttpClient` + `VtuberServerOptions` + `ILogger`），包含：

- `public record VtuberServerOptions(string Endpoint);`（本程序集内重复定义）。
- `SubscribeAsync(string uid)`：`POST {BaseUrl}/api/weibo/subscription`，body `new { uid }`，`EnsureSuccessStatusCode()`。**不提供 UnsubscribeAsync**。
- `GetLatestWeiboAsync(string uid)`：`GET {BaseUrl}/api/weibo/user/{uid}/timeline?page=1&pageSize=1`，try/catch 记日志返回 `null`，正常返回 `response?.Items.FirstOrDefault()`。
- DTO（同文件，System.Text.Json + `[JsonPropertyName]`，字段名与实测接口一致，见技术提案「微博接口实测结构」）：
  - `WeiboTimelineResponse { items: List<WeiboTimelineItem> }`
  - `WeiboTimelineItem { uid: long, mblogId: string, data: WeiboStatus? }`
  - `WeiboStatus { created_at, text, isLongText, pics: List<WeiboPic>, original_pic, user: WeiboUser?, retweeted_status: WeiboStatus? }`
  - `WeiboPic { pid, large: WeiboPicSize? }`、`WeiboPicSize { url }`
  - `WeiboUser { id: long, screen_name }`

### Step 4：`Weibo/WeiboCommandHandler.cs`

仿 `DynamicCommandHandler`（继承 `CommandHandler`，注入 `ICommandDispatcher / IPermission / IBotContext / IJsonConfig<WeiboOptions> / WeiboApi`）：

- 谓词：`text.StartsWith("/微博")` + `IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "weibo.subscribe", ct)`。
- `HandleAsync`：`message.ToTextCommands().First()`，参数数 != 2 时回复帮助。
- 帮助文案：`/微博:订阅:微博用户UID\n/微博:取消:微博用户UID`。
- `订阅`：`await api.SubscribeAsync(uid, token)` 成功后才在 `BeginConfigMutationScopeAsync` 内向 `UidToGroupSubscriptions[uid]` 添加 `groupId`、落盘、回复「已订阅用户{uid}的微博，更新时将会发送微博通知！」；**不初始化 `LastWeiboIds[uid]`**。远端失败抛异常时不改本地配置（回复失败提示或让异常被基类捕获）。
- `取消`：仅本地——从 `UidToGroupSubscriptions[uid]` 移除本群；集合为空则移除 uid 条目并 `LastWeiboIds.Remove(uid)`；落盘；回复「已取消订阅用户{uid}的微博通知！」。**全程不调用远端接口**。未订阅时回复提示即可（B站版无此提示，直接回复取消成功；此处保持简单，与 B站版一致直接回复成功，或加一行未订阅提示，二选一，倾向加提示）。

### Step 5：`Weibo/WeiboSubscriber.cs`

仿 `DynamicSubscriber`（`IExecutable`，注入 `IJsonConfig<WeiboOptions> / WeiboApi / ILogger / IBotContext`）：

- `RunAsync`：`while (!ct.IsCancellationRequested)` 包 try/catch + log。
- `RunAsyncCore`：`config.WaitForInitializedAsync(ct)` → 遍历 `UidToGroupSubscriptions`（空群集合跳过）→ `GetLatestWeiboAsync(uid)` 为 null 或 `Data` 为 null 则 continue → `LastWeiboIds.TryGetValue` 比对 `mblogId`，相同跳过 → 不同则在 mutation scope 内更新游标并落盘 → **仅当旧游标非空**时 `WeiboMessageBuilder.Build(item)`，遍历 `bot.GetAccountInfoAsync` 对每个账号 `WriteManyGroupMessageAsync(accountId, targetGroups, ct, segments)`。
- uid 间 `Task.Delay(_random.Next(1, 3)s)`，整轮结束 `Delay(20s)`。
- 不含 B站的 `DYNAMIC_TYPE_LIVE_RCMD` 过滤。

### Step 6：`Weibo/WeiboMessageBuilder.cs`

静态类，`Build(WeiboTimelineItem item)` → `OutgoingSegment[]`：

1. 首行：`{item.Data.User?.ScreenName ?? "神秘人"} 发布了新微博`。
2. 正文：`HtmlToPlainText(status.Text)`——`<br />`/`<br>`/`</p>` → `\n`；`<a ...>inner</a>` 保留 inner；其余标签用正则剔除；`WebUtility.HtmlDecode`；Trim。
3. 配图：`pics` 非空时正文末尾补 `\n`，逐张取 `pic.Large?.Url`，单图且无 `pics` 时兜底 `OriginalPic`，逐张 `ToMilkyImageSegment()`。
4. 转发：`retweeted_status != null` → 转发语截掉 `//@...` 尾巴（`IndexOf("//@")` 截断），追加 `\n---- 转发 ----\n`，递归渲染原微博为 `@{原博主}: {正文}` 文本段；原微博 `User` 为 null 或正文含「微博已被删除」时输出 `[原微博不可见]`。
5. 末尾文本段附原文链接 `https://m.weibo.cn/status/{mblogId}`。
6. 兜底：清洗后正文为空且无图时输出 `[微博] 暂无可用文本内容`。

### Step 7：`WeiboPlugin.cs`（插件入口）

按技术提案代码注册：`VtuberServerOptions`（环境变量 `Z_VTUBER_SERVER_ENDPOINT`，默认 `http://vtuber.internal.fffdan.com`）、`HttpClient`、`ConfigureJsonConfig("weibo-config.json", WeiboOptions.Default, ct)`、`WeiboApi`、`AddSingletonComponent<WeiboCommandHandler>()`、`AddSingletonExecutable<WeiboSubscriber>()`。

### Step 8：注册到 Core

- `src/ZeroBot.Core/ZeroBot.Core.csproj`：`<ItemGroup>` 内按字母序加 `<ProjectReference Include="..\plugins\ZeroBot.Weibo\ZeroBot.Weibo.csproj" />`（放在 TestPlugin 之后）。
- `src/ZeroBot.Core/Program.cs`：`using ZeroBot.Weibo;`，在 `TypedPluginLoader.Register<BiliBiliPlugin>();`（Program.cs:23）之后加 `TypedPluginLoader.Register<WeiboPlugin>();`。

### Step 9：验证

```bash
dotnet build src/plugins/ZeroBot.Weibo/ZeroBot.Weibo.csproj -c Release
dotnet build src/ZeroBot.Core/ZeroBot.Core.csproj -c Release
```

两者编译通过为实现完成标准。接口字段已由技术提案阶段的真实 curl 验证（单图/多图/转发/纯文本样本），无需重复验证。部署发布（`dotnet publish` + `pm2 restart`）由后续部署节点按根 AGENTS.md 手册执行。

## 验收对照

实现完成后逐条对照 PRD 验收标准 1–9，重点自查：

- 取消订阅代码路径中无任何远端 HTTP 调用（标准 6）；
- 首次轮询只落盘游标不推送（标准 4，逻辑同 `DynamicSubscriber.cs:39`）；
- 最后订阅群取消时清理 `LastWeiboIds[uid]`（标准 7）；
- 清洗后无 `<br`、`<a `、`&amp;` 等 HTML 残留（标准 9）。

## 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/plugins/ZeroBot.Weibo/ZeroBot.Weibo.csproj` | 新增 |
| `src/plugins/ZeroBot.Weibo/WeiboPlugin.cs` | 新增 |
| `src/plugins/ZeroBot.Weibo/WeiboOptions.cs` | 新增 |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboApi.cs` | 新增（含 `VtuberServerOptions` 与全部 DTO） |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboCommandHandler.cs` | 新增 |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboSubscriber.cs` | 新增 |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboMessageBuilder.cs` | 新增 |
| `src/ZeroBot.Core/Program.cs` | 改：注册 `WeiboPlugin` |
| `src/ZeroBot.Core/ZeroBot.Core.csproj` | 改：加项目引用 |
| `docs/plans/weibo-subscription-plan.md` | 新增（本文档） |

实现完成后需同步更新 `src/plugins/AGENTS.md`「现有插件列表」，补充 ZeroBot.Weibo 条目。
