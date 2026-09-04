# B 站主播直播间事件转发 —— 实现方案（Implementation Plan）

| 属性 | 值 |
|---|---|
| 需求分支 | `feat/bilibili-anchor-event-forward` |
| 所属插件 | ZeroBot.Bilibili |
| 关联文档 | [PRD](./bilibili-anchor-event-forward-prd.md) · [技术方案](./bilibili-anchor-event-forward-tech.md) |
| 参照实现 | `src/plugins/ZeroBot.Bilibili/Live/LiveSc*.cs`（模式完全复用） |

---

## 1. 目标

实现 PRD 全部功能：群内指令订阅/取消主播（按 mid）直播间事件，主播本人在自己直播间的**弹幕**与**入场**事件实时转发到订阅 QQ 群。技术路线按技术方案执行，复用 SC 转发的「指令写配置 + Subscriber reconcile 维护 SSE」模式，无新增 NuGet 依赖。

## 2. 前置确认（已核实）

- 需求总分支 `feat/bilibili-anchor-event-forward` 已基于 main（69153f5）并推送 origin。
- `Mikibot.Crawler` 1.0.8 已提供 `DanmuMsg`（自带 `DanmuMsgJsonConverter`）、`InteractWordV2`、`EnterRoomEvent`、`ProtobufCommandExtensions.Parse()`，无需升级。
- `LiveScApi` / `LiveScCommandHandler` / `LiveScSubscriber` / `SuperChatMessageBuilder` 现状与方案描述一致，可直接对照实现。
- 图片消息段构造方式参照 `Video/VideoLinkParser.cs:59` 的 `url.ToMilkyImageSegment()`。

## 3. 实施步骤

按依赖顺序执行，每步完成后编译通过再进入下一步。

### Step 1：配置扩展 —— `src/plugins/ZeroBot.Bilibili/BilibiliOptions.cs`（改）

- 新增 record：`public record AnchorEventSubscription(string RoomId, string UName, HashSet<long> GroupIds);`
- `BilibiliOptions` 增加属性（init 缺省空字典，旧 `bilibili-config.json` 向后兼容）：

  ```csharp
  /// <summary>mid → 主播直播间事件订阅（roomId、主播名、订阅群集合）</summary>
  public Dictionary<string, AnchorEventSubscription> AnchorEventSubscriptions { get; init; } = [];
  ```

### Step 2：API 层 —— `src/plugins/ZeroBot.Bilibili/Live/AnchorEventApi.cs`（新增）

构造函数签名对齐 `LiveScApi`：`(HttpClient http, VtuberServerOptions options, ILogger<AnchorEventApi> logger)`。

- DTO（System.Text.Json，`[JsonPropertyName]` 蛇形映射，与技术方案 2.2 一致）：
  - `VtuberUserInfo { UId, UName, RoomId }`
  - `DanmakuEventItem { RoomId, Payload }` / `DanmakuEventPayload { Cmd, Info: DanmuMsg }`
  - `InteractEventItem { RoomId, Payload }` / `InteractEventPayload { Cmd, Data: InteractWordV2 }`
- `Task<VtuberUserInfo?> GetUserAsync(string mid, CancellationToken)`：`GET {BaseUrl}/api/b/user/{mid}`；404 或异常返回 `null`。
- `Task ReceiveDanmakuEventsAsync(string roomId, Func<DanmuMsg, CancellationToken, Task> handler, CancellationToken)`：
  - `GET {BaseUrl}/api/b/live/{roomId}/event/subscribe?type=danmaku`，`ResponseHeadersRead` 流式读取；SSE 分帧逻辑复制 `LiveScApi`（`data:` 累积、空行派发、收尾 flush）。
  - 反序列化为 `DanmakuEventItem`，`Payload.Cmd == "DANMU_MSG"` 时回调 `Payload.Info`；`JsonException` / `InvalidDataException` 仅 `LogWarning` 跳过，不中断流。
- `Task ReceiveInteractEventsAsync(string roomId, Func<EnterRoomEvent, CancellationToken, Task> handler, CancellationToken)`：
  - 同上，URL `?type=interact`；`Payload.Cmd == "INTERACT_WORD_V2"` 时调用 `Payload.Data.Parse()` 得到 `EnterRoomEvent` 后回调；解析异常同样仅告警跳过。

### Step 3：消息构造 —— `src/plugins/ZeroBot.Bilibili/Live/AnchorEventMessageBuilder.cs`（新增）

静态类，返回 `OutgoingSegment[]`：

- `BuildDanmaku(string anchorName, DanmuMsg msg)`：文本 `{anchorName} 的弹幕 [粉丝牌 等级]：\n{msg.Msg}`；`FansTag` 空白时省略 `[]` 部分（对齐 `SuperChatMessageBuilder` 的 medal 处理）；`msg.MemeUrl` 非空时追加 `msg.MemeUrl.ToMilkyImageSegment()`。
- `BuildEnter(string anchorName)`：`[$"{anchorName} 进入了直播间！".ToMilkyTextSegment()]`。
- 均不 at 全体。

### Step 4：指令处理 —— `src/plugins/ZeroBot.Bilibili/Live/AnchorEventCommandHandler.cs`（新增）

结构对齐 `LiveScCommandHandler`（`CommandHandler`，构造注入 `ICommandDispatcher / IPermission / IBotContext / IJsonConfig<BilibiliOptions>`，另注入 `AnchorEventApi`）：

- 谓词：`text.StartsWith("/主播直播间事件")` 且 `IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "bilibili-anchor-event.subscribe")`。
- 参数个数 != 2 时回复 Help：
  ```
  /主播直播间事件:订阅:B站用户UID
  /主播直播间事件:取消:B站用户UID
  ```
- `订阅`：先 `api.GetUserAsync(mid)` → `null` 回复「未找到该主播，请确认 mid 是否正确」（不进入配置变更）；否则 `BeginConfigMutationScopeAsync` 内 upsert `AnchorEventSubscriptions[mid]`（`RoomId`/`UName` 取自接口，`GroupIds.Add(groupId)`）→ `SaveAsync` → 回复「已订阅主播 {uName}({mid}) 的直播间事件，主播发弹幕/入场时将转发到本群！」（幂等）。**不**直接开 SSE，由 subscriber reconcile。
- `取消`：存在记录则移除本群，`GroupIds` 为空时移除整个 mid 记录 → `SaveAsync` → 回复确认；未订阅过也回复确认，不报错。

### Step 5：订阅器 —— `src/plugins/ZeroBot.Bilibili/Live/AnchorEventSubscriber.cs`（新增）

`IExecutable`，结构对齐 `LiveScSubscriber`，构造注入 `IJsonConfig<BilibiliOptions> / AnchorEventApi / ILogger / IBotContext`：

- `_activeRooms`：`ConcurrentDictionary<string, CancellationTokenSource>`，key 为 roomId。
- `RunAsync`：`WaitForInitializedAsync` 后 5s 循环 `Reconcile`；退出时取消全部 cts。异常处理同 `LiveScSubscriber`（`OperationCanceledException` 退出、其余 `LogError` 继续）。
- `Reconcile`：从 `config.Current.AnchorEventSubscriptions` 聚合出 `roomId` 集合（`GroupIds.Count > 0` 的项）：
  - 有订阅未激活 → `CreateLinkedTokenSource` 后**同时**启动弹幕、入场两条 `ReceiveLoopAsync`（每 roomId 仅一次，两条循环互相独立）；
  - 已激活但无订阅 → `TryRemove` + `Cancel`，两条连接同时断开。
- `ReceiveDanmakuLoopAsync(roomId, ct)` / `ReceiveInteractLoopAsync(roomId, ct)`：`while (!ct.IsCancellationRequested)` 调用对应 `Receive*EventsAsync`，结束后 5s 退避重连；`OperationCanceledException` 退出，其余异常 `LogError` 继续。
- 弹幕回调：遍历当前配置中 `RoomId == roomId` 的订阅项，`msg.UserId.ToString() == mid`（主播本人）时 `BuildDanmaku(uName, msg)`，经 `bot.GetAccountInfoAsync` + `WriteManyGroupMessageAsync` 推送到该 mid 的全部订阅群；否则丢弃。
- 入场回调：同样按 `RoomId` 找订阅项，`enter.Uid.ToString() == mid` 时 `BuildEnter(uName)` 转发。

### Step 6：注册 —— `src/plugins/ZeroBot.Bilibili/BiliBiliPlugin.cs`（改）

在现有 SC 相关注册后追加：

```csharp
services.AddSingleton<AnchorEventApi>();
services.AddSingletonComponent<AnchorEventCommandHandler>();
services.AddSingletonExecutable<AnchorEventSubscriber>();
```

## 4. 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/plugins/ZeroBot.Bilibili/BilibiliOptions.cs` | 改：加 `AnchorEventSubscription` 与 `AnchorEventSubscriptions` |
| `src/plugins/ZeroBot.Bilibili/Live/AnchorEventApi.cs` | 新增 |
| `src/plugins/ZeroBot.Bilibili/Live/AnchorEventMessageBuilder.cs` | 新增 |
| `src/plugins/ZeroBot.Bilibili/Live/AnchorEventCommandHandler.cs` | 新增 |
| `src/plugins/ZeroBot.Bilibili/Live/AnchorEventSubscriber.cs` | 新增 |
| `src/plugins/ZeroBot.Bilibili/BiliBiliPlugin.cs` | 改：注册新服务 |

无新增 NuGet 依赖；不涉及线上配置文件。

## 5. 验证

1. 构建：`dotnet build ZeroBot.slnx -c Release`（或仅 `src/plugins/ZeroBot.Bilibili` 项目）编译通过、无新告警。
2. 测试：`dotnet test test/ZeroBot.Core.Test` 全绿（本次无测试改动，回归即可）。
3. 联调（人工/线上环境）：对照 PRD 第 7 节 8 条验收标准执行，重点：
   - 订阅未收录 mid 回复提示且不写配置；
   - 主播弹幕/入场 5 秒内转发，观众事件不转发；
   - 多群订阅互不影响，全退订后 SSE 断开（日志可证）；
   - 重启后订阅关系自动恢复。

## 6. 备注

- 表情包弹幕（`MemeUrl`）图片段与弹幕文本同一条消息发送。
- `InteractWordV2.Parse()` 失败时库内部会 `Console.WriteLine`，已知并可接受（技术方案第 5 节）。
- 实现中如发现 SSE 分帧逻辑与 `LiveScApi` 高度重复，保持复制不抽象（三个使用点才值得提取公共方法，当前两处）。
