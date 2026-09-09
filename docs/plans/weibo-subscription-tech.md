# 技术提案：微博订阅推送（ZeroBot.Weibo 插件）

- 日期：2026-09-09
- 状态：待评审
- 关联文档：[weibo-subscription-prd.md](weibo-subscription-prd.md)（产品需求）

## 现状分析

### 参照实现：B站动态推送（`src/plugins/ZeroBot.Bilibili/Dynamic/`）

| 文件 | 职责 |
|---|---|
| `Dynamic/DynamicCommandHandler.cs` | `CommandHandler` 子类，`/B站动态:订阅:{mid}`、`:取消:{mid}`；谓词 = 文本前缀 + `IsSudoerOrGroupAdminOrHasPermissionAsync`；`ToTextCommands()` 解析参数、`BeginConfigMutationScopeAsync` 改配置落盘、`ReplyAsGroup` 回复 |
| `Dynamic/DynamicSubscriber.cs` | `IExecutable` 后台轮询：遍历订阅关系 → 拉最新一条 → 游标去重（首次只记录不推送）→ `WriteManyGroupMessageAsync` 群发；mid 间随机延迟 1–3s，整轮后 `Delay(20s)`，外层 `while + try/catch` |
| `Dynamic/VtuberSpaceApi.cs` | 注入 `HttpClient` + `VtuberServerOptions`，`PostAsJsonAsync` / `GetFromJsonAsync<T>`；DTO 用 System.Text.Json + `[JsonPropertyName]`，与 API 类同文件 |
| `Dynamic/DynamicMessageBuilder.cs` | 静态方法，`DynamicData` → `OutgoingSegment[]`（文字段 + 图片段，转发递归） |
| `BilibiliOptions.cs` | 热加载配置（`IJsonConfig<T>`）：`MidToGroupSubscriptions`（订阅关系）+ `LastDynamicIds`（游标） |

端点注入：`Environment.GetEnvironmentVariable("Z_VTUBER_SERVER_ENDPOINT") ?? "http://vtuber.internal.fffdan.com"`。

### 微博接口实测结构（已 curl 验证，2026-09-09）

`GET http://vtuber.internal.fffdan.com/api/weibo/user/{uid}/timeline?page=1&pageSize=1` 返回：

```json
{
  "total": 7, "page": 1, "pageSize": 1,
  "items": [{
    "uid": 9187735727,
    "mblogId": "5341284258023319",
    "fetchedAt": "2026-09-09T09:07:50.409Z",
    "data": {
      "created_at": "Wed Sep 09 17:07:46 +0800 2026",
      "text": "少枝觅食中 ",
      "isLongText": false,
      "pic_ids": ["00a1MNLVgy1igxj4uu54sj30u00u0adp"],
      "pics": [{ "pid": "...", "url": "https://wx2.sinaimg.cn/orj360/....jpg",
                 "large": { "url": "https://wx3.sinaimg.cn/mw2000/....jpg" } }],
      "original_pic": "https://wx3.sinaimg.cn/large/....jpg",
      "user": { "id": 9187735727, "screen_name": "枝堇Sumire" },
      "retweeted_status": { ... 与 data 同构 ... }
    }
  }]
}
```

实测结论：

- `mblogId` 是稳定的微博 ID，可直接做游标。
- `text` 为 HTML 片段：含 `<br />`、`<a href='/n/xxx'>@xxx</a>`、emoji（Unicode 原生字符）等，推送前必须清洗。
- 有图微博必有 `pics[]`（与 `pic_ids` 等长），每项 `large.url` 为大图；单图微博另有 `original_pic` 兜底。
- 转发微博带 `retweeted_status`，结构与 `data` 同构（可递归解析）；转发语的 `text` 里可能含 `//@原作者:原话` 尾巴。
- 订阅接口 `POST /api/weibo/subscription`（body `{"uid":"..."}`）按需求只在订阅时调用，取消不调用远端。
- HTTPS 证书为 Traefik 默认证书（SAN 不匹配），沿用 B站推送的 `http://` 默认端点。

## 实现方案

新建独立插件 `ZeroBot.Weibo`（不动 `ZeroBot.Bilibili`），整体复刻 `Dynamic/` 四件套 + 配置 + 入口。

### 1. 新建项目 `src/plugins/ZeroBot.Weibo/`

按 `src/plugins/AGENTS.md`「创建新插件」：`.csproj`（net10.0，引用 `ZeroBot.Abstraction` + `ZeroBot.Utility`），命名空间 `ZeroBot.Weibo`。

### 2. `WeiboPlugin.cs`（插件入口）

```csharp
public class WeiboPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();

        var endpoint = Environment.GetEnvironmentVariable("Z_VTUBER_SERVER_ENDPOINT")
                       ?? "http://vtuber.internal.fffdan.com";
        services.AddSingleton(new VtuberServerOptions(endpoint));
        services.AddSingleton<HttpClient>();
        services.ConfigureJsonConfig("weibo-config.json", WeiboOptions.Default, cancellationToken);

        services.AddSingleton<WeiboApi>();
        services.AddSingletonComponent<WeiboCommandHandler>();
        services.AddSingletonExecutable<WeiboSubscriber>();
        return ValueTask.FromResult(services);
    }
}
```

`VtuberServerOptions` 无法跨插件复用（Bilibili 的是其程序集内类型），在本插件内重复定义同名 record，保持两插件独立。

### 3. `WeiboOptions.cs`（热加载配置）

```csharp
public record WeiboOptions
{
    public static WeiboOptions Default => new();
    public Dictionary<string, HashSet<long>> UidToGroupSubscriptions { get; init; } = [];
    public Dictionary<string, string> LastWeiboIds { get; init; } = [];  // uid -> mblogId
}
```

配置文件 `weibo-config.json`，经 `IJsonConfig<WeiboOptions>` 热加载。

### 4. `Weibo/WeiboApi.cs`（HTTP + DTO）

DTO（System.Text.Json + `[JsonPropertyName]`，只取需要字段）：

- `WeiboTimelineResponse { total, items: WeiboTimelineItem[] }`
- `WeiboTimelineItem { uid, mblogId, data: WeiboStatus }`
- `WeiboStatus { created_at, text, isLongText, pic_ids, pics: WeiboPic[], original_pic, user: WeiboUser, retweeted_status: WeiboStatus? }`（递归）
- `WeiboPic { pid, large: WeiboPicSize? }`，`WeiboPicSize { url }`
- `WeiboUser { id, screen_name }`

方法：

- `SubscribeAsync(string uid)` → `POST /api/weibo/subscription`，body `{"uid": uid}`；返回 bool（2xx 为成功）。
- `GetLatestWeiboAsync(string uid)` → `GET /api/weibo/user/{uid}/timeline?page=1&pageSize=1`，返回 `items.FirstOrDefault()`；异常/空返回 `null`（try/catch + `logger.LogError`）。

### 5. `Weibo/WeiboCommandHandler.cs`

仿 `DynamicCommandHandler`：

- 谓词：`text.StartsWith("/微博")` + 权限键 `weibo.subscribe`（`IsSudoerOrGroupAdminOrHasPermissionAsync`）。
- 参数解析沿用 `message.ToTextCommands()` + `InvokeCommandAsync` 委托分发；参数不符时回复帮助：
  `/微博:订阅:微博用户UID` / `/微博:取消:微博用户UID`。
- `订阅`：`api.SubscribeAsync(uid)` 成功 → `BeginConfigMutationScopeAsync` 内 `UidToGroupSubscriptions[uid].Add(groupId)` → 落盘 → 回复确认。**不初始化 `LastWeiboIds[uid]`**，首轮轮询只记录不推送。远端调用失败时回复失败提示、不改本地配置。
- `取消`：只改本地配置——移除本群；群集合清空则移除 uid 条目并清理 `LastWeiboIds[uid]` → 落盘 → 回复确认。**不调用任何远端接口**。

### 6. `Weibo/WeiboSubscriber.cs`

仿 `DynamicSubscriber`（`IExecutable`）：

- `while (!ct.IsCancellationRequested)` + try/catch + log。
- 遍历 `config.Current.UidToGroupSubscriptions`：`GetLatestWeiboAsync(uid)` 返回 null → log 并 continue。
- `last = LastWeiboIds.TryGetValue(uid)`；`mblogId` 相同 → 跳过；不同 → 更新游标落盘。
- 通知条件：`last` 非空且与本次不同 → `WeiboMessageBuilder.Build(item)` → 遍历 `bot.GetAccountInfoAsync`，对每个账号 `WriteManyGroupMessageAsync(accountId, groups, ct, segments)`；不 at 全体。
- uid 间随机 `Task.Delay(1~3s)`，整轮结束 `Delay(20s)`。

### 7. `Weibo/WeiboMessageBuilder.cs`

`WeiboTimelineItem` → `OutgoingSegment[]`：

- 首行：`{user.screen_name} 发布了新微博\n`。
- 正文：`HtmlToPlainText(text)`：
  - `<br />`、`<br>`、`</p>` 等块级标签 → `\n`；
  - `<a ...>inner</a>` → 保留 inner 文本（链接文字即 `@xxx`、话题等，链接本身丢失可接受，原文链接兜底）；
  - 其余标签剔除；`System.Net.WebUtility.HtmlDecode` 解码实体；末尾 Trim。
- 配图：`pics` 非空时正文末尾补 `\n`，逐张 `pic.large.url`（兜底 `original_pic`）`ToMilkyImageSegment()`。
- 转发：`retweeted_status` 非空 → 先发清洗后的转发语（截掉 `//@...` 尾巴），追加 `\n---- 转发 ----\n`，递归渲染原微博（`@{原博主}: {正文}`）；原微博 `user` 缺失或文本含「微博已被删除」等异常时输出 `[原微博不可见]` 兜底。
- 末尾附原文链接 `https://m.weibo.cn/status/{mblogId}`。
- 兜底：清洗后正文为空且无图时输出 `[微博] 暂无可用文本内容`。

### 8. 注册到 Core

- `src/ZeroBot.Core/Program.cs`：`using ZeroBot.Weibo;` + `TypedPluginLoader.Register<WeiboPlugin>();`
- `src/ZeroBot.Core/ZeroBot.Core.csproj`：添加 `ProjectReference`。

### 9. 验证

- `dotnet build src/plugins/ZeroBot.Weibo/ZeroBot.Weibo.csproj -c Release` 及全量 `dotnet build src/ZeroBot.Core/ZeroBot.Core.csproj -c Release` 编译通过。
- 接口字段已在调研阶段用真实 curl 验证（单图/多图/转发/纯文本样本，`pageSize=7`）。
- 按 AGENTS.md 手册发布：`dotnet publish src/ZeroBot.Core/ZeroBot.Core.csproj -c Release` 后 `pm2 restart ZeroBot --update-env`（由部署节点执行）。

## 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/plugins/ZeroBot.Weibo/ZeroBot.Weibo.csproj` | 新增 |
| `src/plugins/ZeroBot.Weibo/WeiboPlugin.cs` | 新增（插件入口） |
| `src/plugins/ZeroBot.Weibo/WeiboOptions.cs` | 新增（热加载配置） |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboApi.cs` | 新增（含 `VtuberServerOptions`、DTO） |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboCommandHandler.cs` | 新增 |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboSubscriber.cs` | 新增 |
| `src/plugins/ZeroBot.Weibo/Weibo/WeiboMessageBuilder.cs` | 新增 |
| `src/ZeroBot.Core/Program.cs` | 改：注册 `WeiboPlugin` |
| `src/ZeroBot.Core/ZeroBot.Core.csproj` | 改：加项目引用 |
| `docs/plans/weibo-subscription-prd.md` | 新增（产品需求） |
| `docs/plans/weibo-subscription-tech.md` | 新增（本文档） |

## 关键决策点

1. **独立插件而非扩展 Bilibili**：微博与 B站是两个业务域，独立插件 + 独立配置（`weibo-config.json`）避免耦合；`VtuberServerOptions` 宁可重复定义也不跨程序集引用 Bilibili。
2. **取消订阅不调远端接口**：严格按需求执行；服务端订阅残留由服务端自行管理（本地停止轮询后不再产生流量，残留订阅无实际影响）。
3. **轮询只取 `pageSize=1`**：与 B站推送一致，游标去重天然漏不掉（微博按时间倒序，新微博必然出现在第一条）；极端情况下一轮内连发多条只推最新一条，可接受。
4. **HTML 清洗而非富文本解析**：微博 `text` 是 HTML 片段，简单标签替换 + 实体解码即可，不引入 HTML 解析库。
5. **图片用 `pics[].large.url`**：实测单图/多图微博均有 `pics[]`，`original_pic` 仅作单图兜底。
6. **首次订阅不回溯推送**：游标首次只记录，与 B站推送行为一致，避免订阅瞬间轰炸。
