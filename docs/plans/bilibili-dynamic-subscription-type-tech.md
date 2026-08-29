# 技术提案：B站动态订阅 API type 参数适配

- 日期：2026-08-29
- 状态：待评审
- 关联文档：[bilibili-dynamic-subscription-type-prd.md](bilibili-dynamic-subscription-type-prd.md)（产品需求）

## 现状分析

后台 API 调用集中在 `src/plugins/ZeroBot.Bilibili/Dynamic/VtuberSpaceApi.cs`，当前实现：

```csharp
// SubscribeAsync：body 缺少 type，服务端返回 400
await http.PostAsJsonAsync($"{BaseUrl}/api/b/subscription", new { mid }, cancellationToken);

// UnsubscribeAsync：未携带 type
await http.DeleteAsync($"{BaseUrl}/api/b/subscription/{mid}", cancellationToken);
```

调用方为 `Dynamic/DynamicCommandHandler.cs`（`/B站动态:订阅|取消` 指令），其自身逻辑无需改动。

## 接口实测结论（已 curl 验证）

对 `http://vtuber.internal.fffdan.com` 实测：

| 请求 | 结果 |
|------|------|
| `POST /api/b/subscription` body `{"mid":"2"}` | **400** |
| `POST /api/b/subscription` body `{"mid":"2","type":"dynamic"}` | **200**，返回 `{"mid":2,"type":"dynamic","subscribedAt":null}` |
| `DELETE /api/b/subscription/2?type=dynamic` | **204** |
| `DELETE /api/b/subscription/2` body `{"type":"dynamic"}` | **400** |

结论：

1. 订阅的 `type` 放在 **JSON body**；
2. 取消订阅的 `type` 必须放在 **query string**，body 方式不被接受。

## 实现方案

仅修改 `VtuberSpaceApi.cs` 一个文件，两个方法各改一行：

```csharp
public async Task SubscribeAsync(string mid, CancellationToken cancellationToken = default)
{
    var response = await http.PostAsJsonAsync(
        $"{BaseUrl}/api/b/subscription", new { mid, type = "dynamic" }, cancellationToken);
    response.EnsureSuccessStatusCode();
}

public async Task UnsubscribeAsync(string mid, CancellationToken cancellationToken = default)
{
    var response = await http.DeleteAsync(
        $"{BaseUrl}/api/b/subscription/{mid}?type=dynamic", cancellationToken);
    response.EnsureSuccessStatusCode();
}
```

要点：

- `type` 固定为 `"dynamic"` 字面量：该 API 类当前只服务动态订阅，暂无参数化必要；后续接入其他事件类型时再抽象。
- 不引入配置项、不改 DTO、不改 `DynamicCommandHandler` / `DynamicSubscriber` / `BilibiliOptions`。
- 失败行为不变：`EnsureSuccessStatusCode` 抛异常由 `DynamicCommandHandler` 的既有调用链上抛，与现状一致。

## 影响面

| 文件 | 动作 |
|------|------|
| `src/plugins/ZeroBot.Bilibili/Dynamic/VtuberSpaceApi.cs` | 改：`SubscribeAsync` / `UnsubscribeAsync` 各加 type 参数 |

无配置变更、无数据库/存储迁移、无其他插件影响。

## 验证

1. `dotnet build src/plugins/ZeroBot.Bilibili/ZeroBot.Bilibili.csproj -c Release` 编译通过；
2. 接口行为已在调研阶段用真实 curl 验证（见上表）；
3. 部署后实测 `/B站动态:订阅:{mid}` 与 `/B站动态:取消:{mid}` 群内回复正常。
