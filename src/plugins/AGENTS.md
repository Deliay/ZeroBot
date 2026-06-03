# ZeroBot Plugins

## 目录

- [项目架构](#项目架构)
- [创建新插件](#创建新插件)
- [现有插件列表](#现有插件列表)

---

## 项目架构

ZeroBot 基于 [EmberFramework](https://github.com/Deliay/EmberFramework) 构建，采用插件化架构。

```
src/
├── ZeroBot.Abstraction/    # 抽象层：接口定义 (IBotService, IServiceManager, IPermission 等)
├── ZeroBot.Core/           # 核心层：启动入口、服务注册、命令分发
├── ZeroBot.Utility/        # 工具层：扩展方法、命令处理器基类、配置热加载
└── plugins/                # 插件目录
```

### 核心概念

| 概念 | 说明 |
|------|------|
| `IPlugin` | 插件入口接口，实现 `BuildComponents()` 注册服务 |
| `IPlugin.IWithInitializer` | 带初始化回调的插件，额外实现 `InitializeAsync()` |
| `IComponentInitializer` | 组件初始化器，框架自动调用 |
| `IExecutable` | 可执行后台任务，框架自动调用 `RunAsync()` |
| `CommandHandler` | 命令处理器基类，处理匹配的消息 |
| `CommandQueuedHandler` | 带队列的命令处理器，串行处理消息 |
| `MessageQueueHandler<T>` | 消息队列处理器，监听所有消息并排队处理 |
| `IJsonConfig<T>` | 热加载 JSON 配置接口 |

### 工具方法 (ZeroBot.Utility)

```csharp
// 注册单例组件 (实现 IComponentInitializer)
services.AddSingletonComponent<MyComponent>();

// 注册单例可执行任务 (实现 IExecutable)
services.AddSingletonExecutable<MyTask>();

// 注册带接口的可执行任务
services.AddSingletonExecutable<IMyInterface, MyTask>();

// 注册热加载 JSON 配置
services.ConfigureJsonConfig("config.json", MyConfig.Default, cancellationToken);
```

---

## 创建新插件

### 步骤 1: 创建项目目录和文件

```bash
mkdir src/plugins/ZeroBot.MyPlugin
```

### 步骤 2: 创建 .csproj 文件

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\ZeroBot.Abstraction\ZeroBot.Abstraction.csproj" />
        <ProjectReference Include="..\..\ZeroBot.Utility\ZeroBot.Utility.csproj" />
    </ItemGroup>

    <!-- 按需添加第三方包 -->
</Project>
```

### 步骤 3: 实现插件入口类

```csharp
using EmberFramework.Abstraction.Layer.Plugin;
using Microsoft.Extensions.DependencyInjection;
using ZeroBot.Utility;

namespace ZeroBot.MyPlugin;

public class MyPlugin : IPlugin
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        IServiceCollection services = new ServiceCollection();

        // 注册热加载配置
        services.ConfigureJsonConfig("my-config.json", MyConfig.Default, cancellationToken);

        // 注册组件 (自动初始化)
        services.AddSingletonComponent<MyComponent>();

        // 注册后台任务 (自动运行)
        services.AddSingletonExecutable<MyBackgroundTask>();

        // 注册普通服务
        services.AddSingleton<MyService>();

        return ValueTask.FromResult(services);
    }
}
```

如果需要初始化回调，实现 `IPlugin.IWithInitializer`:

```csharp
public class MyPlugin : IPlugin.IWithInitializer
{
    public ValueTask<IServiceCollection> BuildComponents(CancellationToken cancellationToken = default)
    {
        // ... 同上
    }

    public ValueTask InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // 插件初始化逻辑
        return ValueTask.CompletedTask;
    }
}
```

### 步骤 4: 实现组件/命令

**命令处理器 (响应特定命令):**

```csharp
using ZeroBot.Abstraction.Bot;
using ZeroBot.Utility;

namespace ZeroBot.MyPlugin;

public class MyCommand(ICommandDispatcher dispatcher, IBotContext bot)
    : CommandQueuedHandler(dispatcher)
{
    protected override ValueTask<bool> PredicateAsync(
        Event<IncomingMessage> message, CancellationToken cancellationToken = default)
    {
        // 判断是否匹配命令
        return ValueTask.FromResult(message.ToText().Trim() == "/mycommand");
    }

    protected override ValueTask DequeueAsync(
        Event<IncomingMessage> @event, CancellationToken cancellationToken = default)
    {
        // 处理命令
        return @event.ReplyAsGroup(bot, cancellationToken,
            ["响应内容".ToMilkyTextSegment()]);
    }
}
```

**后台任务 (持续运行):**

```csharp
using EmberFramework.Abstraction;

namespace ZeroBot.MyPlugin;

public class MyBackgroundTask : IExecutable
{
    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // 执行后台逻辑
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }
}
```

### 步骤 5: 注册到 Core

在 `src/ZeroBot.Core/Program.cs` 中添加:

```csharp
using ZeroBot.MyPlugin;

// 添加插件注册
TypedPluginLoader.Register<MyPlugin>();
```

在 `src/ZeroBot.Core/ZeroBot.Core.csproj` 中添加项目引用:

```xml
<ProjectReference Include="..\plugins\ZeroBot.MyPlugin\ZeroBot.MyPlugin.csproj" />
```

### 步骤 6: 添加配置文件 (可选)

在运行目录创建 JSON 配置文件，框架会自动热加载:

```json
{
  "key": "value"
}
```

---

## 现有插件列表

### ZeroBot.Milky

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Milky/` |
| 状态 | **核心插件，必需** |
| 命名空间 | `ZeroBot.Milky` |

**功能:** Bot 通信层，基于 [Milky 协议](https://github.com/ZeroAsh/ZeroAsh.Milky.Net.Client) 实现 QQ 机器人消息收发。

**注册的服务:**
- `IBotService` (MilkyBot) - Bot 核心服务，提供消息发送、群管理等 API
- `MilkyHttpClient` - HTTP 客户端
- `MilkyWebSocketReceiver` - WebSocket 消息接收器

**依赖:** `ZeroAsh.Milky.Net.Client`

---

### ZeroBot.AI

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.AI/` |
| 命名空间 | `ZeroBot.AI` |

**功能:** AI Agent 系统，集成 OpenAI API 实现群聊 AI 对话、TRPG 技能检定等功能。

**注册的服务:**
- `ChatProviderManager` - AI 模型提供商管理
- `GenericOpenAIProvider` - 通用 OpenAI 兼容 API 提供商
- `AgentManager` / `AgentSessionManager` - Agent 会话管理
- `GroupAgentManager` - 群组 Agent 管理
- `SkillManager` - 技能系统管理
- `TrpgCommandHandler` - TRPG 命令处理器

**依赖:** `OpenAI`, `Microsoft.Agents.AI.Foundry`, `Microsoft.Agents.AI.OpenAI`

---

### ZeroBot.Bilibili

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Bilibili/` |
| 命名空间 | `ZeroBot.Bilibili` |

**功能:** B 站集成，提供视频链接解析和直播状态监控。

**组件:**
- `VideoLinkParser` - 自动解析群聊中的 B 站视频链接 (BV/AV/b23.tv)，返回封面和标题
- `LiveStatusSubscriber` - 监控 B 站直播间开播状态，自动通知订阅群组
- `LiveStatutCommandHandler` - 直播状态查询命令

**配置文件:** `bilibili-config.json`

**依赖:** `Mikibot.Crawler`, `HtmlAgilityPack`

---

### ZeroBot.Endfield

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Endfield/` |
| 命名空间 | `ZeroBot.Endfield` |

**功能:** 《明日方舟：终末地》游戏集成，提供森空岛账号绑定、签到、游戏信息查询。

**组件:**
- `HypergraphyCommand` - 主命令路由器 (`/鹰角`, `/zmd` 前缀)
- `BindingCommandHandlers` - 账号绑定/解绑/自动签到管理
- `EndfieldCommandHandlers` - 终末地游戏信息查询 (理智、日常、干员)
- `DailySignPeriodicTask` - 每日自动签到定时任务
- `ScanQrCodeTaskManager` - 二维码扫码登录管理
- `PuzzleSolver` - 终末地解谜题求解器 (`/解题`)

**命令:**
- `/鹰角:绑定` - 私聊，绑定森空岛账号
- `/鹰角:已绑` - 私聊，查看已绑定账号
- `/鹰角:解绑:本地ID` - 私聊，解绑账号
- `/鹰角:自动签到:本地ID` - 开启自动签到
- `/zmd` 或 `/zmd:我的信息` - 查看终末地角色信息
- `/zmd:我的干员` - 查看干员信息
- `/解题` - 发送棋盘图片求解

**配置文件:** `puzzle.json`, `sign_settings.json`, `sign_credentials.json`

**依赖:** `ZeroBot.Endfield.Api`, `ZeroBot.Endfield.Credential.Json`, `QRCoder`

---

### ZeroBot.Endfield.Api

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Endfield.Api/` |
| 命名空间 | `ZeroBot.Endfield.Api` |

**功能:** 森空岛/鹰角网络 API 客户端库，提供认证、玩家信息、签到等 API 封装。

**核心类:**
- `HypergryphClient` - HTTP 客户端
- `CredentialManager` - 凭证管理器 (OAuth Token、扫码登录)
- `ICredentialRepository` - 凭证存储接口

**依赖:** `Polly` (重试策略)

---

### ZeroBot.Endfield.Credential.Json

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Endfield.Credential.Json/` |
| 命名空间 | `ZeroBot.Endfield.Credential.Json` |

**功能:** 基于 JSON 文件的凭证存储实现。

**核心类:**
- `JsonCredentialRepository` - 实现 `ICredentialRepository`，将凭证存储到 JSON 文件

---

### ZeroBot.Endfield.Card.BrowserRender

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Endfield.Card.BrowserRender/` |
| 命名空间 | `ZeroBot.Endfield.Card.BrowserRender` |
| 状态 | **未启用** (在 Program.cs 中被注释) |

**功能:** 浏览器渲染服务，用于生成终末地角色卡片图片。

**组件:**
- `RenderApiServer` - 渲染 API 服务器
- `RenderContextProvider` - 渲染上下文管理

**配置文件:** `render.json`

---

### ZeroBot.Endfield.Card.BrowserRender.Abstraction

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Endfield.Card.BrowserRender.Abstraction/` |
| 命名空间 | `ZeroBot.Endfield.Card.BrowserRender.Abstraction` |

**功能:** 渲染服务抽象层，定义 `IRenderContextProvider` 接口。

---

### ZeroBot.Endfield.Playground

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Endfield.Playground/` |
| 命名空间 | `ZeroBot.Endfield.Playground` |
| 状态 | **独立控制台程序** (非插件) |

**功能:** 终末地 API 测试/调试工具，独立运行的控制台程序。

---

### ZeroBot.ComfyUI

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.ComfyUI/` |
| 命名空间 | `ZeroBot.ComfyUI` |

**功能:** ComfyUI 图像生成集成，通过 ComfyUI 工作流处理图片。

**组件:**
- `ToAkumaria` - 图片风格转换命令 (`/变毬`)，将图片转换为特定风格

**配置文件:** 环境变量配置 ComfyUI 端点

**依赖:** `ZeroAsh.ComfySharp`

---

### ZeroBot.PermissionCommandPlugin

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.PermissionCommandPlugin/` |
| 命名空间 | `ZeroBot.PermissionCommandPlugin` |

**功能:** 权限管理命令，允许群管理员启用/禁用群功能。

**组件:**
- `PermissionManagerCommand` - 权限设置命令处理器

**命令:**
- `/$权限设置:g:enable:{perm}` - 启用群功能
- `/$权限设置:g:disable:{perm}` - 禁用群功能

---

### ZeroBot.Repository.Mongo

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Repository.Mongo/` |
| 命名空间 | `ZeroBot.Repository.Mongo` |

**功能:** MongoDB 数据存储层，提供事件持久化存储。

**注册的服务:**
- `MongoRepository` - MongoDB 仓库实现
- `IMongoClient` - MongoDB 客户端

**实现接口:** `IPlugin.IWithInitializer`，初始化时将 `MongoRepository` 设置为 `IBotContext` 的事件仓库。

**依赖:** `MongoDB.Driver`

---

### ZeroBot.Meme

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.Meme/` |
| 命名空间 | `ZeroBot.Meme` |
| 状态 | **未实现** (抛出 `NotImplementedException`) |

**功能:** 表情包生成 (计划中)。

---

### ZeroBot.TestPlugin

| 属性 | 值 |
|------|-----|
| 路径 | `src/plugins/ZeroBot.TestPlugin/` |
| 命名空间 | `ZeroBot.TestPlugin` |

**功能:** 测试插件，包含各种示例组件。

**组件:**
- `Ping` - `/ping` 命令，回复 "pong"
- `Wish` - 祈愿模拟
- `Boots` - 启动任务
- `FakeNapCat` - 模拟 NapCat 行为
- `Emotions` - 表情处理
- `AutoAcceptFriend` - 自动接受好友请求

**配置文件:** `boat.json`, `emotion_settings.json`
