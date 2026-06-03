# 终末地版本更新订阅功能

## 概述

在 `ZeroBot.Endfield` 插件中增加版本更新订阅功能，群聊可通过指令 `/zmd:更新订阅` 订阅终末地版本更新通知。后台每小时轮询一次版本接口，检测到版本变更时向已订阅群组推送通知。

---

## 配置设计

新增配置文件 `endfield_version_subscription.json`，使用 `IJsonConfig<T>` 热加载。

```csharp
// src/plugins/ZeroBot.Endfield/Config/EndfieldVersionSubscriptionConfig.cs

namespace ZeroBot.Endfield.Config;

public record EndfieldVersionSubscriptionConfig(
    HashSet<long> SubscribedGroupIds,
    string? LastKnownVersion)
{
    public static EndfieldVersionSubscriptionConfig Empty => new([], null);
}
```

| 字段 | 说明 |
|------|------|
| `SubscribedGroupIds` | 已订阅版本更新的群 ID 集合 |
| `LastKnownVersion` | 上次已知的版本号，首次运行时为 null |

---

## 组件设计

### 1. 版本轮询后台任务

**文件:** `src/plugins/ZeroBot.Endfield/Component/EndfieldVersionPollingTask.cs`

```csharp
namespace ZeroBot.Endfield.Component;

public class EndfieldVersionPollingTask(
    IJsonConfig<EndfieldVersionSubscriptionConfig> config,
    HttpClient httpClient,
    ILogger<EndfieldVersionPollingTask> logger,
    IBotContext bot) : IExecutable
{
    private const string VersionEndpoint = "https://endfield-assets.fffdan.com/version";
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        await config.WaitForInitializedAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Version polling failed");
            }
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private async ValueTask PollAsync(CancellationToken cancellationToken)
    {
        var version = await httpClient.GetStringAsync(VersionEndpoint, cancellationToken);

        if (config.Current.LastKnownVersion is null)
        {
            // 首次运行，仅记录版本，不发送通知
            await SaveVersionAsync(version, cancellationToken);
            return;
        }

        if (version == config.Current.LastKnownVersion) return;

        // 版本变更，发送通知
        await NotifySubscribersAsync(version, cancellationToken);
        await SaveVersionAsync(version, cancellationToken);
    }

    private async ValueTask NotifySubscribersAsync(string newVersion, CancellationToken cancellationToken)
    {
        if (config.Current.SubscribedGroupIds.Count == 0) return;

        var message = $"终末地版本更新！新版本: {newVersion}".ToMilkyTextSegment();
        foreach (var (accountId, _) in bot.GetAccountInfoAsync(cancellationToken))
        {
            await bot.WriteManyGroupMessageAsync(
                accountId,
                config.Current.SubscribedGroupIds,
                cancellationToken,
                message);
        }
    }

    private ValueTask SaveVersionAsync(string version, CancellationToken cancellationToken)
    {
        return config.BeginConfigMutationScopeAsync((value, token) =>
        {
            var updated = value with { LastKnownVersion = version };
            return config.SaveAsync(updated, token);
        }, cancellationToken);
    }
}
```

### 2. 订阅命令处理器

**文件:** `src/plugins/ZeroBot.Endfield/Component/EndfieldVersionSubscriptionCommand.cs`

在 `HypergraphyCommand` 中扩展 `/zmd` 路由，新增 `更新订阅` 子命令。

或者独立为一个 CommandHandler，挂在 `/zmd:更新订阅` 指令上。

**方案：扩展 HypergraphyCommand**

在 `HypergraphyCommand.EndfieldCommandDispatchAsync` 中增加路由：

```csharp
private ValueTask EndfieldCommandDispatchAsync(
    Event<IncomingMessage> @event,
    ITextCommand cmd,
    CancellationToken cancellationToken = default)
{
    if (cmd.Arguments.Length == 0) return endfield.MyEndfieldInfoAsync(@event, cancellationToken);
    return cmd.Arguments[0] switch
    {
        "我的信息" => endfield.MyEndfieldInfoAsync(@event, cancellationToken),
        "我的干员" => endfield.MyEndfieldCharacterInfoAsync(@event, cancellationToken),
        "更新订阅" => versionSubscription.ToggleSubscriptionAsync(@event, cancellationToken),
        _ => HelpAsync(@event, cancellationToken)
    };
}
```

帮助文本中增加：

```
/zmd:更新订阅  (订阅/取消订阅终末地版本更新通知)
```

### 3. 订阅逻辑实现

**文件:** `src/plugins/ZeroBot.Endfield/Component/EndfieldVersionSubscriptionCommand.cs`

```csharp
namespace ZeroBot.Endfield.Component;

public class EndfieldVersionSubscriptionCommand(
    IJsonConfig<EndfieldVersionSubscriptionConfig> config,
    IBotContext bot)
{
    public async ValueTask ToggleSubscriptionAsync(Event<IncomingMessage> message,
        CancellationToken cancellationToken = default)
    {
        var groupId = message.Data.PeerId;

        await config.BeginConfigMutationScopeAsync(async (value, token) =>
        {
            var subscribed = value.SubscribedGroupIds.Contains(groupId);
            if (subscribed)
            {
                value.SubscribedGroupIds.Remove(groupId);
                await message.ReplyAsGroup(bot, token,
                    ["已取消订阅终末地版本更新通知".ToMilkyTextSegment()]);
            }
            else
            {
                value.SubscribedGroupIds.Add(groupId);
                await message.ReplyAsGroup(bot, token,
                    ["已订阅终末地版本更新通知，版本变更时将自动通知本群".ToMilkyTextSegment()]);
            }
            await config.SaveAsync(value, token);
        }, cancellationToken);
    }
}
```

---

## 插件注册

在 `EndfieldPlugin.BuildComponents` 中新增：

```csharp
// 版本订阅配置
services.ConfigureJsonConfig("endfield_version_subscription.json",
    EndfieldVersionSubscriptionConfig.Empty, cancellationToken);

// 版本轮询后台任务
services.AddSingletonExecutable<EndfieldVersionPollingTask>();
services.AddSingleton<HttpClient>();

// 订阅命令
services.AddSingleton<EndfieldVersionSubscriptionCommand>();

// HypergraphyCommand 需注入 EndfieldVersionSubscriptionCommand
```

---

## 文件变更清单

| 操作 | 文件 |
|------|------|
| 新增 | `src/plugins/ZeroBot.Endfield/Config/EndfieldVersionSubscriptionConfig.cs` |
| 新增 | `src/plugins/ZeroBot.Endfield/Component/EndfieldVersionPollingTask.cs` |
| 新增 | `src/plugins/ZeroBot.Endfield/Component/EndfieldVersionSubscriptionCommand.cs` |
| 修改 | `src/plugins/ZeroBot.Endfield/Component/HypergraphyCommand.cs` - 增加 `更新订阅` 路由和帮助文本 |
| 修改 | `src/plugins/ZeroBot.Endfield/EndfieldPlugin.cs` - 注册新服务 |

---

## 运行流程

```
应用启动
  ├─ EndfieldPlugin.BuildComponents 注册所有服务
  ├─ EndfieldVersionPollingTask.RunAsync 启动
  │   ├─ 等待配置初始化
  │   └─ 进入轮询循环
  │       ├─ GET https://endfield-assets.fffdan.com/version
  │       ├─ 首次运行 → 记录版本，不通知
  │       ├─ 版本未变 → 跳过
  │       ├─ 版本变更 → 向所有订阅群发送通知 → 更新配置
  │       └─ 等待 1 小时
  └─ 用户发送 /zmd:更新订阅
      └─ ToggleSubscriptionAsync
          ├─ 已订阅 → 移除群 ID → 回复"已取消"
          └─ 未订阅 → 添加群 ID → 回复"已订阅"
```

---

## 参考实现

- 轮询模式参考：`src/plugins/ZeroBot.Bilibili/Live/LiveStatusSubscriber.cs`
- 配置热加载参考：`IJsonConfig<T>` 用法，见 `SklandDailySignConfig`
- 群消息发送参考：`IBotContext.WriteManyGroupMessageAsync`
- 命令路由参考：`HypergraphyCommand.EndfieldCommandDispatchAsync`
