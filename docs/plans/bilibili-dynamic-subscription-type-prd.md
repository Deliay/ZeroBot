# 产品需求文档：B站动态订阅 API type 参数适配

- 日期：2026-08-29
- 状态：待评审
- 关联文档：[bilibili-dynamic-subscription.md](bilibili-dynamic-subscription.md)（动态订阅功能初版方案）、[bilibili-dynamic-subscription-type-tech.md](bilibili-dynamic-subscription-type-tech.md)（技术提案）

## 背景

ZeroBot.Bilibili 插件已上线「B站动态订阅」功能：群聊发送 `/B站动态:订阅:{mid}` 后，Bot 调用后台 vtuber server API 订阅该用户的动态更新事件，并由 `DynamicSubscriber` 轮询推送新动态到订阅群；`/B站动态:取消:{mid}` 取消订阅。

后台 vtuber server 的订阅 API 已升级：订阅与取消订阅接口现在**必须携带 `type` 参数**以区分订阅的事件类型（动态 / 直播等）。当前 Bot 侧的调用未携带该参数：

- `POST /api/b/subscription` 请求体仅有 `{"mid": "..."}`，服务端返回 **400**，订阅失败；
- `DELETE /api/b/subscription/{mid}` 未携带任何 type 信息，同样无法正确取消动态类型的订阅。

导致现象：用户执行 `/B站动态:订阅` 后收不到订阅成功回复（API 抛异常），功能不可用。

## 需求

### R1：订阅指令携带 type 参数

群聊发送 `/B站动态:订阅:{B站用户UID}` 时，Bot 调用后台订阅 API 的请求体必须包含 `type: dynamic`：

```
POST {endpoint}/api/b/subscription
Content-Type: application/json

{"mid": "{mid}", "type": "dynamic"}
```

不携带 `type` 时服务端返回 400（已实测验证）。

### R2：取消订阅指令携带 type 参数

群聊发送 `/B站动态:取消:{B站用户UID}` 时，Bot 调用后台取消订阅 API 必须以 **query 参数** 携带 `type=dynamic`：

```
DELETE {endpoint}/api/b/subscription/{mid}?type=dynamic
```

注：实测 DELETE 请求以 JSON body 携带 `{"type":"dynamic"}` 会被服务端拒绝（400），type 必须放在 query string 中。

### R3：行为兼容性

- 指令交互形式、权限校验（`bilibili-dynamic.subscribe`）、本地配置结构（`BilibiliOptions`）均不变；
- 仅动态订阅（`/B站动态`）涉及本次变更；若后续接入其他事件类型（如直播），复用同一 `type` 机制。

## 验收标准

| # | 场景 | 预期 |
|---|------|------|
| 1 | 群聊 `/B站动态:订阅:{mid}` | 后台 API 返回 2xx，群内收到订阅成功回复 |
| 2 | 群聊 `/B站动态:取消:{mid}`（最后一个订阅群取消） | 后台 API 返回 2xx，群内收到取消成功回复 |
| 3 | 订阅请求抓包/日志检查 | 请求体含 `"type":"dynamic"` |
| 4 | 取消订阅请求抓包/日志检查 | URL 含 `?type=dynamic` |

## 非目标

- 不新增其他事件类型的订阅指令；
- 不改动动态轮询推送（`DynamicSubscriber`）与消息渲染（`DynamicMessageBuilder`）逻辑；
- 不改动后台 vtuber server 本身。
