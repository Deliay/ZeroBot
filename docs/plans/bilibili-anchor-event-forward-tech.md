# B 站主播直播间事件转发 —— 技术方案

| 属性 | 值 |
|---|---|
| 所属插件 | ZeroBot.Bilibili |
| 关联文档 | [产品需求文档（PRD）](./bilibili-anchor-event-forward-prd.md) |
| 参照实现 | [直播 SC 转发](./bilibili-live-sc-forward.md)（`Live/LiveSc*.cs`，模式完全复用） |

---

## 1. 调研结论（已 curl 验证）

以下均基于线上 vtuber server（`http://vtuber.internal.fffdan.com`）实测。

### 1.1 mid → roomId

`GET /api/b/user/{mid}` 返回主播信息（以 mid=1150976664 为例）：

```json
{
  "uId": 1150976664,
  "uName": "枝堇Sumire",
  "roomId": 1727076670,
  "faceUrl": "https://...",
  "isLiving": false,
  "title": "蝉鸣，夏日末",
  "...": "..."
}
```

- 关键字段：`uId` / `uName` / `roomId`。
- **未被收录的用户（非主播）返回 404**（RFC9110 problem+json），订阅时据此校验 mid 合法性。

### 1.2 danmaku 事件结构

`GET /api/b/live/{roomId}/event/range?page=1&pageSize=1&type=danmaku` 返回 `items[0]`（SSE 实时数据同格式）：

```json
{
  "roomId": 1727076670,
  "type": "danmaku",
  "occurredAt": "2026-09-04T17:05:20.112Z",
  "liveSessionId": "...",
  "payload": {
    "cmd": "DANMU_MSG",
    "dm_v2": "",
    "info": [[0,1,25,...], "(●'◡'●)ﾉ♥", [521594619, "子不语Kazy"], [19, "小粉螈", "枝堇Sumire", ...], ...]
  }
}
```

- 该事件流仅含 `cmd == "DANMU_MSG"`（抽样 200 条验证）。
- `payload.info` 为 B 站弹幕原始数组结构，**可直接用 `Mikibot.Crawler` 的 `DanmuMsg` 反序列化**（其 `DanmuMsgJsonConverter` 正是按该数组布局解析）：

  ```csharp
  // Mikibot.Crawler.WebsocketCrawler.Data.Commands.KnownCommand.DanmuMsg
  public struct DanmuMsg : IKnownCommand
  {
      public string Msg { get; set; }          // 弹幕内容
      public long UserId { get; set; }         // 发送者 mid ← 主播过滤依据
      public string UserName { get; set; }     // 发送者昵称
      public string FansTag { get; set; }      // 粉丝牌名
      public int FansLevel { get; set; }       // 粉丝牌等级
      public DateTimeOffset SentAt { get; set; }
      public string MemeUrl { get; set; }      // 表情包弹幕图片 URL（无则空串）
      ...
  }
  ```

- 判定主播本人弹幕：`msg.UserId == 订阅的 mid`。

### 1.3 入场事件结构（interact）

`GET /api/b/live/{roomId}/event/range?page=1&pageSize=1&type=interact` 返回 `items[0]`（SSE 实时数据同格式）：

```json
{
  "roomId": 1727076670,
  "type": "interact",
  "occurredAt": "2026-09-04T17:09:36.102Z",
  "payload": {
    "cmd": "INTERACT_WORD_V2",
    "data": { "dmscore": 10, "pb": "CL2QgNnqiJMG...(base64 protobuf)" }
  }
}
```

`payload.data` 可直接反序列化为 `Mikibot.Crawler` 的 `InteractWordV2`，再调用其扩展方法 `Parse()`（`ProtobufCommandExtensions`，base64 → protobuf 解码）得到 `EnterRoomEvent`：

```csharp
// Mikibot.Crawler.WebsocketCrawler.Data.Commands.KnownCommand.ProtoCommand
public struct InteractWordV2 : IProtobufCommand<EnterRoomEvent>
{
    [JsonPropertyName("dmscore")] public int DamakuScore { get; set; }
    [JsonPropertyName("pb")] public string ProtobufData { get; set; }
}

public partial class EnterRoomEvent
{
    public long Uid { get; set; }          // 入场者 mid ← 主播过滤依据
    public string Name { get; set; }       // 入场者昵称
    public long LiveRoomId { get; set; }
    public FansMedal Medal { get; set; }   // { Uid, Level, Name, LiveRoomId }
}
```

- 已确认项目引用的 `Mikibot.Crawler` 1.0.8（`Directory.Packages.props`）包含上述类型与 `Parse` 扩展方法，**无需升级依赖**。
- 判定主播本人入场：`enter.Uid == 订阅的 mid`。

### 1.4 SSE 订阅

`GET /api/b/live/{roomId}/event/subscribe?type=danmaku` 与 `?type=interact` 均为 SSE（`text/event-stream`）长连接，按 `data:` 行推送，格式与 `event/range` 的 `items[0]` 一致。解析方式与 `LiveScApi.ReceiveSuperChatEventsAsync` 完全相同，仅事件 DTO 不同。

---

## 2. 实现方案

整体复用 SC 转发的「指令写配置 + Subscriber reconcile 维护 SSE」模式。

### 2.1 配置扩展（`BilibiliOptions.cs`）

```csharp
public record AnchorEventSubscription(string RoomId, string UName, HashSet<long> GroupIds);

public record BilibiliOptions(...)
{
    ...
    /// <summary>mid → 主播直播间事件订阅（roomId、主播名、订阅群集合）</summary>
    public Dictionary<string, AnchorEventSubscription> AnchorEventSubscriptions { get; init; } = [];
}
```

- key 为 mid 字符串；向后兼容旧 `bilibili-config.json`（缺省为空字典）。
- 以 mid 为 key 可保证「取消订阅」无需再次请求接口；`RoomId`/`UName` 冗余存储供转发与回复使用。

### 2.2 新增文件（`Live/` 目录）

#### `Live/AnchorEventApi.cs`（用户查询 + SSE）

- DTO：
  ```csharp
  public class VtuberUserInfo
  {
      [JsonPropertyName("uId")] public long UId { get; set; }
      [JsonPropertyName("uName")] public string UName { get; set; } = "";
      [JsonPropertyName("roomId")] public long RoomId { get; set; }
  }
  public class DanmakuEventItem
  {
      [JsonPropertyName("roomId")] public long RoomId { get; set; }
      [JsonPropertyName("payload")] public DanmakuEventPayload? Payload { get; set; }
  }
  public class DanmakuEventPayload
  {
      [JsonPropertyName("cmd")] public string Cmd { get; set; } = "";
      [JsonPropertyName("info")] public DanmuMsg Info { get; set; }   // 走 DanmuMsgJsonConverter
  }
  public class InteractEventItem
  {
      [JsonPropertyName("roomId")] public long RoomId { get; set; }
      [JsonPropertyName("payload")] public InteractEventPayload? Payload { get; set; }
  }
  public class InteractEventPayload
  {
      [JsonPropertyName("cmd")] public string Cmd { get; set; } = "";
      [JsonPropertyName("data")] public InteractWordV2 Data { get; set; }
  }
  ```
- `Task<VtuberUserInfo?> GetUserAsync(string mid, CancellationToken)`：`GET {endpoint}/api/b/user/{mid}`；404 / 异常返回 `null`。
- `Task ReceiveDanmakuEventsAsync(string roomId, Func<DanmuMsg, CancellationToken, Task> handler, CancellationToken)`：
  - `GET {endpoint}/api/b/live/{roomId}/event/subscribe?type=danmaku`，`ResponseHeadersRead` 流式读取；
  - SSE 分帧逻辑与 `LiveScApi` 一致（`data:` 累积、空行派发）；
  - 反序列化为 `DanmakuEventItem`，`payload.cmd == "DANMU_MSG"` 时回调 `payload.info`；`JsonException` 仅告警不中断。
- `Task ReceiveInteractEventsAsync(string roomId, Func<EnterRoomEvent, CancellationToken, Task> handler, CancellationToken)`：
  - `GET {endpoint}/api/b/live/{roomId}/event/subscribe?type=interact`，SSE 分帧逻辑同上；
  - 反序列化为 `InteractEventItem`，`payload.cmd == "INTERACT_WORD_V2"` 时调用 `payload.data.Parse()`（`ProtobufCommandExtensions` 扩展方法，base64 → protobuf）得到 `EnterRoomEvent` 后回调；JSON / protobuf 解析异常仅告警不中断。

> 注：`DanmuMsg` 标注了 `[JsonConverter(typeof(DanmuMsgJsonConverter))]`，作为属性反序列化时 System.Text.Json 会自动使用该 converter 读取 `info` 数组，无需手写解析。

#### `Live/AnchorEventCommandHandler.cs`

- 谓词：`text.StartsWith("/主播直播间事件")` + 权限 `bilibili-anchor-event.subscribe`（`IsSudoerOrGroupAdminOrHasPermissionAsync`）。
- `订阅`：`api.GetUserAsync(mid)` → `null` 回复「未找到该主播，请确认 mid 是否正确」；否则在 `BeginConfigMutationScopeAsync` 内 upsert `AnchorEventSubscriptions[mid]`（RoomId/UName 取自接口，`GroupIds.Add(groupId)`）→ `SaveAsync` → 回复确认。**不**直接开 SSE（由 subscriber 统一管理）。
- `取消`：移除本群；`GroupIds` 为空则移除该 mid 记录 → `SaveAsync` → 回复确认。
- 参数个数不为 2 时回复 Help：

  ```
  /主播直播间事件:订阅:B站用户UID
  /主播直播间事件:取消:B站用户UID
  ```

#### `Live/AnchorEventSubscriber.cs`（IExecutable）

结构对齐 `LiveScSubscriber`：

- `RunAsync`：`WaitForInitializedAsync` 后 5s 循环 `Reconcile`，比对 `config.Current.AnchorEventSubscriptions`（聚合出 roomId 集合）与 `_activeRooms`（`ConcurrentDictionary<string, CancellationTokenSource>`，key 为 roomId）：
  - 有订阅未激活 → `CreateLinkedTokenSource` 同时启动弹幕与入场两条 `ReceiveLoopAsync`（每 roomId 仅一次）；
  - 已激活但无订阅 → `Cancel` 同时断开两条连接。
- `ReceiveLoopAsync(roomId, ct)`：`while (!ct.IsCancellationRequested)` 内 `try { ReceiveDanmakuEventsAsync(...) / ReceiveInteractEventsAsync(...) } catch (OperationCanceledException) { break; } catch { log }` + 5s 退避重连。弹幕与入场各一条循环，互不影响。
- 收到弹幕 → 遍历当前配置中 `RoomId == roomId` 的订阅项，`msg.UserId.ToString() == mid` 时 `AnchorEventMessageBuilder.BuildDanmaku(uName, msg)`，经 `bot.GetAccountInfoAsync` + `WriteManyGroupMessageAsync` 推送给该 mid 的全部订阅群。
- 收到入场 → 同样按 `RoomId` 找订阅项，`enter.Uid.ToString() == mid` 时 `AnchorEventMessageBuilder.BuildEnter(uName)` 转发给该 mid 的全部订阅群。

#### `Live/AnchorEventMessageBuilder.cs`

```csharp
public static OutgoingSegment[] BuildDanmaku(string anchorName, DanmuMsg msg)
public static OutgoingSegment[] BuildEnter(string anchorName)
```

- 弹幕文本：`{anchorName} 的弹幕 [粉丝牌 等级]：\n{msg.Msg}`；`FansTag` 为空时省略 `[]`。
- `msg.MemeUrl` 非空时追加图片消息段（参照 `VideoLinkParser` 中图片段的构造方式）。
- 入场文本：`{anchorName} 进入了直播间！`（纯文本，不 at 全体）。

### 2.3 注册（`BiliBiliPlugin.cs`）

```csharp
services.AddSingleton<AnchorEventApi>();
services.AddSingletonComponent<AnchorEventCommandHandler>();
services.AddSingletonExecutable<AnchorEventSubscriber>();
```

---

## 3. 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/plugins/ZeroBot.Bilibili/BilibiliOptions.cs` | 改：加 `AnchorEventSubscriptions` 与 `AnchorEventSubscription` |
| `src/plugins/ZeroBot.Bilibili/Live/AnchorEventApi.cs` | 新增（用户查询 + danmaku/interact SSE + DTO） |
| `src/plugins/ZeroBot.Bilibili/Live/AnchorEventCommandHandler.cs` | 新增（订阅/取消指令） |
| `src/plugins/ZeroBot.Bilibili/Live/AnchorEventSubscriber.cs` | 新增（reconcile + 双接收循环 + 过滤转发） |
| `src/plugins/ZeroBot.Bilibili/Live/AnchorEventMessageBuilder.cs` | 新增（弹幕/入场消息构造） |
| `src/plugins/ZeroBot.Bilibili/BiliBiliPlugin.cs` | 改：注册新服务 |
| `docs/plans/bilibili-anchor-event-forward-prd.md` | 新增（PRD） |
| `docs/plans/bilibili-anchor-event-forward-tech.md` | 新增（本文档） |

无新增 NuGet 依赖（`Mikibot.Crawler` 已在 `ZeroBot.Bilibili.csproj` 中引用，`DanmuMsg` / `InteractWordV2` / `EnterRoomEvent` / `Parse` 扩展均属于该包，1.0.8 已包含）。

## 4. 关键决策点

1. **主播过滤在转发侧做**：danmaku / interact 流含全量观众事件，订阅侧无法按发送者过滤，因此每条事件判断 `UserId / Uid == mid`，观众事件直接丢弃（判定成本可忽略）。
2. **配置以 mid 为 key、冗余 roomId/uName**：取消订阅与转发均无需再请求接口；SSE 生命周期按聚合出的 roomId reconcile，与 SC 转发一致（每 roomId 弹幕、入场各一条长连接，空集合即断）。
3. **mid 合法性在订阅时校验**：`/api/b/user/{mid}` 对未收录用户返回 404，订阅即失败提示，避免无效订阅挂着空 SSE。
4. **`DanmuMsg` 直接作为 JSON 属性反序列化**：利用其自带 `JsonConverter` 解析 `payload.info` 数组，不引入自研解析。
5. **`InteractWordV2.Parse()` 解析入场事件**：`payload.data` 反序列化为 `InteractWordV2` 后调用扩展方法 `Parse()` 解码 protobuf 得到 `EnterRoomEvent`，复用 Mikibot.Crawler 现有能力，不自研 protobuf 解析。
6. **表情包弹幕带图**：`MemeUrl` 非空追加图片段；弹幕与入场均不 at 全体。

## 5. 风险与缓解

| 风险 | 缓解 |
|---|---|
| 主播高频发言刷屏 | 主播自身弹幕频率天然低；如后续需要可加最小转发间隔，本期不做 |
| `DanmuMsg` converter 对个别异常数组抛 `InvalidDataException` | 单条 try/catch 告警跳过，不中断事件流 |
| `Parse()` 对个别异常 pb 数据解码失败 | 单条 try/catch 告警跳过；注意该扩展方法失败时会 `Console.WriteLine`，可接受 |
| interact 事件量大（全量观众入场） | 仅做 `Uid` 比较后即丢弃，解析失败的单条跳过；若成为瓶颈可后续优化为先比 Uid 再完整处理 |
| mid 对应 roomId 变更（换号直播） | 重新执行订阅指令即可刷新 roomId |
| 大订阅量下 SSE 连接数 | 每 roomId 两条连接（弹幕+入场），与 SC 转发同量级，可接受 |
