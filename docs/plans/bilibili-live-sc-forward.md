# B站直播 SC 转发 + 动态直播类型解析（ZeroBot.Bilibili 插件）

## 需求

1. `DYNAMIC_TYPE_LIVE_RCMD` 动态解析为「正在直播」通知。
2. 仿照「B站动态订阅」，新增「直播 SC 转发」，指令 `/直播SC:订阅:{roomId}`、`/直播SC:取消:{roomId}`：
   - 通过 SSE 订阅直播间 `super_chat` 事件（每个 roomId 只订阅一次）。
   - 仅当某 roomId 全部退订时才取消接收，否则 `while + try/catch` 一直接收。
3. 「B站动态」的取消订阅接口调用改为：仅当该 mid 没有任何群监控时才调用 `DELETE`。

---

## 调研结论（已 curl 验证）

### LIVE_RCMD 动态结构

`GET /api/b/user/{mid}/space?page=1&pageSize=1` 返回的 `items[0].data`：

```json
{
  "type": "DYNAMIC_TYPE_LIVE_RCMD",
  "modules": {
    "module_dynamic": {
      "major": {
        "type": "MAJOR_TYPE_LIVE_RCMD",
        "live_rcmd": {
          "content": "{\"type\":1,\"live_play_info\":{\"room_id\":1727076670,\"title\":\"...\",\"cover\":\"https://...\",\"link\":\"//live.bilibili.com/1727076670?live_from=85002\",\"online\":135930,\"area_name\":\"虚拟日常\"}}"
        },
        "opus": null
      }
    }
  }
}
```

关键点：`live_rcmd.content` 是一个 **JSON 字符串**，内部 `live_play_info` 含 `title` / `cover` / `link` / `room_id` / `online` / `area_name`。

### super_chat 事件结构

`GET /api/b/live/{roomId}/event/range?page=1&pageSize=1&type=super_chat` 返回 `items[0]`：

```json
{
  "roomId": 1727076670,
  "type": "super_chat",
  "occurredAt": "2026-08-15T06:27:27.036Z",
  "liveSessionId": "...",
  "payload": {
    "cmd": "SUPER_CHAT_MESSAGE",
    "data": {
      "message": "妈妈你答应我们的zdgy奥数你就奥数不要忘记了",
      "price": 2,
      "user_info": { "uname": "哈基米系数证明人", "face": "https://..." },
      "medal_info": { "medal_name": "小粉螈", "medal_level": 8 }
    }
  }
}
```

解析所需字段：`payload.data.message`（内容）、`payload.data.price`（金额/元）、`payload.data.user_info.uname`（用户）、`payload.data.medal_info.medal_name` / `medal_level`（粉丝牌）。

### SSE 订阅接口

`GET /api/b/live/{roomId}/event/subscribe?type=super_chat` 为 SSE（`text/event-stream`）长连接，按 `data:` 行推送 super_chat 事件（格式与 `event/range` 的 `items[0]` 一致）。连接保持期间无事件时不返回数据，断开/出错后需重连。

---

## 实现方案

### 1. 配置扩展（`BilibiliOptions.cs`）

```csharp
public Dictionary<string, HashSet<long>> ScRoomIdToGroupSubscriptions { get; init; } = [];
```

用于 roomId → 订阅群集合，向后兼容旧 `bilibili-config.json`。

### 2. LIVE_RCMD 解析

**`Dynamic/VtuberSpaceApi.cs`** 扩展 DTO：

- `DynamicMajor` 增加 `live_rcmd`；新增 `DynamicLiveRcmd { content }`、`LiveRcmdContent { live_play_info }`、`LivePlayInfo { title, cover, link, room_id, online, area_name }`。

**`Dynamic/DynamicMessageBuilder.cs`**：

- `AppendDynamic` 开头判断 `data.Type == "DYNAMIC_TYPE_LIVE_RCMD"` → `AppendLiveRcmd`：
  - 解析 `live_rcmd.content` JSON，取 `title` / `cover` / `link`。
  - 文本 `[正在直播] {title}\n{link}`，随后配图 `cover`。

### 3. SC 转发（`Live/` 新增文件）

#### `Live/LiveScApi.cs`（SSE + DTO）

- DTO：`LiveEventItem { roomId, type, payload }`、`LiveEventPayload { cmd, data }`、`SuperChatData { message, price, user_info, medal_info }`、`SuperChatUser { uname, face }`、`SuperChatMedal { medal_name, medal_level }`。
- `ReceiveSuperChatEventsAsync(roomId, Func<SuperChatData, CancellationToken, Task> handler, ct)`：
  - `GET {endpoint}/api/b/live/{roomId}/event/subscribe?type=super_chat`，`ResponseHeadersRead` 流式读取。
  - 逐行解析 SSE：`data:` 行累积，空行派发；`data` JSON 反序列化为 `LiveEventItem`，取 `payload.data` 回调。

#### `Live/LiveScCommandHandler.cs`

- 谓词：`text.StartsWith("/直播SC")` + 权限 `bilibili-sc.subscribe`。
- `订阅`：`ScRoomIdToGroupSubscriptions[roomId].Add(groupId)` → 落盘 → 回复。**不**直接开 SSE（由 subscriber 统一管理）。
- `取消`：移除本群；集合为空则移除 roomId → 落盘 → 回复。

#### `Live/LiveScSubscriber.cs`（IExecutable）

- `RunAsync` 循环（5s）调用 `ReconcileAsync`：比对 `config.Current.ScRoomIdToGroupSubscriptions` 与 `_activeRooms`（`ConcurrentDictionary<string, CancellationTokenSource>`）。
  - 有群订阅但未激活 → `CreateLinkedTokenSource` 并启动 `ReceiveLoopAsync`（每个 roomId 只启动一次）。
  - 已激活但群集合为空/已移除 → `cts.Cancel()` 取消接收。
- `ReceiveLoopAsync(roomId, ct)`：`while(!ct.IsCancellationRequested)` 内 `try { ReceiveSuperChatEventsAsync } catch(OperationCanceledException){break} catch{log}` + 5s 退避重连。
- 收到 SC → `SuperChatMessageBuilder.Build` → 遍历 `bot.GetAccountInfoAsync` → `WriteManyGroupMessageAsync` 推送给 roomId 的全部订阅群。

#### `Live/SuperChatMessageBuilder.cs`

`{uname} 的SC ¥{price} [{medal_name} {medal_level}]：\n{message}`。

### 4. 动态取消订阅改为「空集合才 DELETE」（`Dynamic/DynamicCommandHandler.cs`）

`取消` 分支：先移除本群；仅当 `subscriptions.Count == 0` 时再 `api.UnsubscribeAsync(mid)` 并清理 `LastDynamicIds`。

### 5. 注册（`BiliBiliPlugin.cs`）

```csharp
services.AddSingleton<LiveScApi>();
services.AddSingletonComponent<LiveScCommandHandler>();
services.AddSingletonExecutable<LiveScSubscriber>();
```

---

## 文件变更清单

| 文件 | 动作 |
|---|---|
| `src/plugins/ZeroBot.Bilibili/BilibiliOptions.cs` | 改：加 `ScRoomIdToGroupSubscriptions` |
| `src/plugins/ZeroBot.Bilibili/Dynamic/VtuberSpaceApi.cs` | 改：加 LIVE_RCMD DTO |
| `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicMessageBuilder.cs` | 改：加 LIVE_RCMD 渲染 |
| `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicCommandHandler.cs` | 改：取消订阅仅空集合才 DELETE |
| `src/plugins/ZeroBot.Bilibili/Live/LiveScApi.cs` | 新增（SSE + super_chat DTO） |
| `src/plugins/ZeroBot.Bilibili/Live/LiveScCommandHandler.cs` | 新增 |
| `src/plugins/ZeroBot.Bilibili/Live/LiveScSubscriber.cs` | 新增 |
| `src/plugins/ZeroBot.Bilibili/Live/SuperChatMessageBuilder.cs` | 新增 |
| `src/plugins/ZeroBot.Bilibili/BiliBiliPlugin.cs` | 改：注册新服务 |
| `docs/plans/bilibili-live-sc-forward.md` | 新增（本文档） |

## 关键决策点

1. SC 转发无独立 `POST/DELETE` 订阅接口，SSE 由 `LiveScSubscriber` 按 roomId 统一维护（每 roomId 一个长连接，配置变化时 reconcile）。
2. 退订以「群集合为空」为准，取消 CTS 即断开 SSE；否则重连循环保持接收。
3. LIVE_RCMD 的 `cover` 直接作为图片消息段，`link` 归一化（`//` → `https:`）。
4. SC 消息不 at 全体（SC 较频繁）。
5. SSE 解析兼容 `data:` 前缀与空行分帧，反序列化失败仅告警不中断。
