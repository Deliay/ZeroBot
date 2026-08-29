# 实现方案：B站动态订阅 API type 参数适配

- 日期：2026-08-29
- 状态：待实施
- 关联文档：[bilibili-dynamic-subscription-type-prd.md](bilibili-dynamic-subscription-type-prd.md)（产品需求）、[bilibili-dynamic-subscription-type-tech.md](bilibili-dynamic-subscription-type-tech.md)（技术提案）

## 目标

实现 PRD 中 R1 / R2：`/B站动态:订阅:{mid}` 与 `/B站动态:取消:{mid}` 调用后台 vtuber server API 时携带 `type=dynamic` 参数，满足 R3（行为兼容，除 API 调用外无任何行为变化）。

## 实施约束

- **禁止对 `http://vtuber.internal.fffdan.com` 做实测**：该地址为线上服务，接口行为结论以技术提案中调研阶段的 curl 实测记录为准，实施与验证阶段不得再向其发起请求。
- 变更范围严格限定在技术提案的影响面内：仅 `VtuberSpaceApi.cs` 一个文件、两个方法。
- 不新增配置项、不改 DTO、不改 `DynamicCommandHandler` / `DynamicSubscriber` / `BilibiliOptions`。
- 不修改任何部署配置文件（pm2 生态文件、JSON 配置）。

## 实施步骤

### 步骤 1：修改 `VtuberSpaceApi.cs`

文件：`src/plugins/ZeroBot.Bilibili/Dynamic/VtuberSpaceApi.cs`

1. `SubscribeAsync`（第 15 行）：请求体匿名对象加入 `type = "dynamic"`：

```csharp
var response = await http.PostAsJsonAsync(
    $"{BaseUrl}/api/b/subscription", new { mid, type = "dynamic" }, cancellationToken);
```

2. `UnsubscribeAsync`（第 21 行）：URL 追加 query 参数 `?type=dynamic`：

```csharp
var response = await http.DeleteAsync(
    $"{BaseUrl}/api/b/subscription/{mid}?type=dynamic", cancellationToken);
```

要点：

- `type` 使用 `"dynamic"` 字面量，不做参数化（该类当前仅服务动态订阅，后续接入其他事件类型时再抽象）；
- `EnsureSuccessStatusCode()` 失败上抛行为不变，由 `DynamicCommandHandler` 既有调用链处理。

### 步骤 2：编译验证

```bash
dotnet build src/plugins/ZeroBot.Bilibili/ZeroBot.Bilibili.csproj -c Release
```

要求编译通过、无新增警告。

### 步骤 3：单元/集成验证策略

- 项目现有测试目录 `test/ZeroBot.Core.Test/` 不含 Bilibili 插件测试，本次变更不新增测试工程（变更仅为两行请求参数，接口行为已由调研阶段 curl 实测确认，见技术提案）。
- **不调用线上服务做实测**。验证方式为代码走查核对：
  - 订阅请求体序列化结果含 `"type":"dynamic"`（验收标准 #3）；
  - 取消订阅 URL 含 `?type=dynamic`（验收标准 #4）。

### 步骤 4：构建与部署（合入后由发布流程执行）

```bash
dotnet clean src/ZeroBot.Core/ZeroBot.Core.csproj -c Release && dotnet publish src/ZeroBot.Core/ZeroBot.Core.csproj -c Release
pm2 restart ZeroBot --update-env
```

- publish 输出目录固定为 `src/ZeroBot.Core/bin/Release/net10.0/publish/`，不得修改 `-o` 参数；
- 严禁 `pm2 delete`；
- 部署后观察日志 `pm2 log ZeroBot --lines 30`，确认无异常。

### 步骤 5：验收（部署后，线上行为观察）

| # | 场景 | 预期 | 验证方式 |
|---|------|------|----------|
| 1 | 群聊 `/B站动态:订阅:{mid}` | 群内收到订阅成功回复 | 用户实际操作 |
| 2 | 群聊 `/B站动态:取消:{mid}` | 群内收到取消成功回复 | 用户实际操作 |
| 3 | 订阅请求体 | 含 `"type":"dynamic"` | 代码走查（步骤 3 已完成） |
| 4 | 取消订阅 URL | 含 `?type=dynamic` | 代码走查（步骤 3 已完成） |

## 风险与回滚

- **风险**：极低。变更仅为两个 HTTP 请求的参数补充，失败行为与现状一致（异常上抛、群内提示失败），不影响其他功能。
- **回滚**：`git revert` 本变更提交后重新 publish + `pm2 restart ZeroBot --update-env` 即可，无配置/数据迁移。

## 交付清单

| 项 | 内容 |
|----|------|
| 代码变更 | `src/plugins/ZeroBot.Bilibili/Dynamic/VtuberSpaceApi.cs`（2 行） |
| 分支 | `feat/bilibili-dynamic-subscription-type` |
| 关联 PR | https://github.com/Deliay/ZeroBot/pull/3 |
