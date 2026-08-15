# B站动态更新订阅功能计划（ZeroBot.Bilibili 插件）

## 需求

仿照「开播状态订阅」（`/直播状态:订阅:房间号`），实现「B站动态更新订阅」：

1. 注入环境变量 `Z_VTUBER_SERVER_ENDPOINT`，默认值 `http://vtuber.internal.fffdan.com`
2. 群聊发送 `/B站动态:订阅:{mid}` → `POST {endpoint}/api/b/subscription`（body `{"mid":"{mid}"}`），并开启动态推送
3. 每 20 秒拉取 `GET {endpoint}/api/b/user/{mid}/space?page=1&pageSize=1` 获取最新动态；拉取失败持续重试
4. 通知逻辑：上一次记录不为空、且本次 dynamicId 与上一次不同 → 推送动态内容
5. 通知内容：解析 `module_dynamic` 富文本为 Milky 消息段；`opus.pics` 配图在文字之后换行展示
6. 群聊发送 `/B站动态:取消:{mid}` → `DELETE {endpoint}/api/b/subscription/{mid}`，并停止该 mid 的轮询推送

## 调研结论（已验证）

### 现有「开播状态」模式（仿照对象）

- `Live/LiveStatutCommandHandler.cs`：`CommandHandler` 子类，`PredicateAsync` 用文本前缀 `/直播状态` + 权限 `IsSudoerOrGroupAdminOrHasPermissionAsync`；`HandleAsync` 用 `message.ToTextCommands()` 解析 `:订阅:xxx` 参数，`config.BeginConfigMutationScopeAsync` 内改配置并 `SaveAsync` 落盘，`message.ReplyAsGroup` 回复。
- `Live/LiveStatusSubscriber.cs`：`IExecutable` 后台任务（框架自动执行 `RunAsync`），`while + Task.Delay` 轮询；首次只记录状态不通知；异常 catch + log 继续。
- 配置：`BilibiliOptions`（`bilibili-config.json` 热加载，`IJsonConfig<T>`）。
- 消息：`OutgoingSegment[]`，`string.ToMilkyTextSegment()` / `string.ToMilkyImageSegment()`（`ZeroBot.Utility/EventExtensions.cs`），推送用 `bot.WriteManyGroupMessageAsync(accountId, groups, ct, segments)`。

### vtuber 接口实测结构（已 curl 验证）

- `GET {endpoint}/api/b/user/{mid}/space?page=1&pageSize=1` →
  `{ total, page, pageSize, items: [{ mid, dynamicId, fetchedAt, data: { id_str, type, modules: { module_dynamic: {...} }, orig } }] }`
- `data.type` 实测有 `DYNAMIC_TYPE_DRAW`（图文）和 `DYNAMIC_TYPE_FORWARD`（转发）。
- 图文内容在 `module_dynamic.major.opus`：
  - `title`（可空）、`summary.text` / `summary.rich_text_nodes[]`（节点 `type` 有 `RICH_TEXT_NODE_TYPE_TEXT`、`RICH_TEXT_NODE_TYPE_EMOJI` 等）、`pics[]`（`{ url, width, height }`，url 为 `http://i0.hdslb.com/...`）、`jump_url`（`//www.bilibili.com/opus/{id}`）。
- 转发的转发语在 `module_dynamic.desc`（同 summary 结构），原动态在 `data.orig`（与 `data` 同构，递归解析）。
- 长文示例（page=11）确认：长文本都在 `summary.text` 内（含 `\n`），配图在 `opus.pics`。
- Mikibot.Crawler 包没有动态相关 API，需自己写 HTTP 调用；插件已注册 `HttpClient` 单例可直接注入。
- `Z_VTUBER_SERVER_ENDPOINT` 仓内无先例，属新引入。

## 实现方案

### 1. 端点注入（`BiliBiliPlugin.cs`）

```csharp
var endpoint = Environment.GetEnvironmentVariable("Z_VTUBER_SERVER_ENDPOINT")
               ?? "http://vtuber.internal.fffdan.com";
services.AddSingleton(new VtuberServerOptions(endpoint));
```

### 2. 扩展配置（`BilibiliOptions.cs`）

以 init 属性追加，旧 `bilibili-config.json` 缺字段时反序列化走初始值，向后兼容：

```csharp
public Dictionary<string, HashSet<long>> MidToGroupSubscriptions { get; init; } = [];
public Dictionary<string, string> LastDynamicIds { get; init; } = [];
```

### 3. 新增 `Dynamic/VtuberSpaceApi.cs`（HTTP + DTO）

- DTO 用 System.Text.Json + `JsonPropertyName`（蛇形命名），只取需要的字段：
  `VtuberSpaceResponse → items[] → { dynamicId, data }`；`DynamicData { type, modules, orig }`（orig 递归）；
  `modules.module_dynamic → { desc, major }`；`major.opus → { title, summary, pics[], jump_url }`；
  `RichText { text, rich_text_nodes[] }`；pics 取 `url`。
- 方法（注入 `HttpClient` + `VtuberServerOptions`）：
  - `SubscribeAsync(mid)` → `POST /api/b/subscription`
  - `UnsubscribeAsync(mid)` → `DELETE /api/b/subscription/{mid}`
  - `GetLatestDynamicAsync(mid)` → `GET /api/b/user/{mid}/space?page=1&pageSize=1`，返回 `items.FirstOrDefault()`；失败/空返回 `null`

### 4. 新增 `Dynamic/DynamicCommandHandler.cs`

仿 `LiveStatutCommandHandler`：

- 谓词：`text.StartsWith("/B站动态")` + 权限键 `bilibili-dynamic.subscribe`
- `订阅`：`api.SubscribeAsync(mid)` → `MidToGroupSubscriptions[mid].Add(groupId)` → 落盘 → 回复确认。**不**初始化 `LastDynamicIds[mid]`：首轮拉取只记录不通知，正好满足「上一次不为空才通知」。
- `取消`：`api.UnsubscribeAsync(mid)` → 移除本群；空集合则移除 mid 并清理 `LastDynamicIds[mid]` → 落盘 → 回复确认。轮询器每轮读 `config.Current`，配置移除后该 mid 自然停止轮询。
- 帮助：`/B站动态:订阅:B站用户UID` / `/B站动态:取消:B站用户UID`

### 5. 新增 `Dynamic/DynamicSubscriber.cs`

仿 `LiveStatusSubscriber` 的 `IExecutable` 模式：

- 遍历 `MidToGroupSubscriptions`：`GetLatestDynamicAsync` 返回 null（拉取失败/无动态）→ log 并 continue（持续拉取）
- `last = LastDynamicIds.TryGetValue(mid)`；id 相同 → 跳过；否则更新 `LastDynamicIds[mid]` 并落盘
- 通知条件：`last` 非空且与本次不同 → 构建消息并推送
- 每个 mid 之间随机延迟 1–3s；整轮结束 `Task.Delay(20s)`；外层 `while + try/catch`

### 6. 新增 `Dynamic/DynamicMessageBuilder.cs`

`module_dynamic` → `OutgoingSegment[]`：

- 文本：富文本节点拼接（TEXT/EMOJI/其他均取节点 `text`，emoji 即 `[UPOWER_...]` 占位文本）；`opus.title` 非空放首行；`jump_url` 归一化（`//` 前缀补 `https:`）附在文本末尾
- 配图：`opus.pics` 非空时文本末尾补 `\n`，随后每张图一个 `ImageOutgoingSegment`；url 做 `http://` → `https://` 归一化
- `DYNAMIC_TYPE_FORWARD`：先发 `desc`（转发语），追加 `\n---- 转发 ----\n`，递归渲染 `orig`
- 兜底：无 opus 且无 desc 时发 `[{type}] 暂无可用文本内容`
- 推送：遍历 `bot.GetAccountInfoAsync`，对每个账号 `WriteManyGroupMessageAsync`；**不 at 全体**

### 7. 注册（`BiliBiliPlugin.cs`）

```csharp
services.AddSingleton<VtuberSpaceApi>();
services.AddSingletonComponent<DynamicCommandHandler>();
services.AddSingletonExecutable<DynamicSubscriber>();
```

### 8. 验证

- `dotnet build src/plugins/ZeroBot.Bilibili/ZeroBot.Bilibili.csproj -c Release` 编译通过
- 接口字段已在调研阶段用真实 curl 验证（page 1/2/4/8/11）

## 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/plugins/ZeroBot.Bilibili/BiliBiliPlugin.cs` | 改：env 注入 + 注册新服务 |
| `src/plugins/ZeroBot.Bilibili/BilibiliOptions.cs` | 改：加两个字典属性 |
| `src/plugins/ZeroBot.Bilibili/Dynamic/VtuberSpaceApi.cs` | 新增（含 `VtuberServerOptions`、DTO） |
| `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicCommandHandler.cs` | 新增 |
| `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicSubscriber.cs` | 新增 |
| `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicMessageBuilder.cs` | 新增 |
| `docs/plans/bilibili-dynamic-subscription.md` | 新增（本文档） |

## 关键决策点

1. 环境变量直接 `Environment.GetEnvironmentVariable` 读取（仓内无先例，最简单且满足「注入+默认值」）。
2. 配置复用 `bilibili-config.json` 的 `BilibiliOptions`，init 属性保证向后兼容。
3. 取消订阅的「停止轮询」靠配置移除实现，不单独管理 per-mid Timer。
4. 动态通知不 at 全体（动态比开播频繁）。
5. emoji 节点按占位文本处理，不转图片。
