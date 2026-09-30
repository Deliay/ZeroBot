# 实现方案：语音合成插件（ZeroBot.Synthesize）

- 日期：2026-10-01
- 状态：待实施
- 关联文档：[synthesize-voice-tech.md](synthesize-voice-tech.md)（技术提案）

## 目标

落地技术提案 R1–R5：别名管理指令 + 群聊语音合成指令 + 动态可配地址 + UTC+8 每日 3 条限流。

## 实施约束

- 变更严格限定在技术提案「影响面」表格内。
- **不修改任何现有部署配置文件**（pm2 生态文件、既有 JSON 配置）；插件自身新增的 `synthesize-config.json` 由框架在首次运行时自动创建，不手工改动。
- 不修改 `CommandDispatcher`、`Permission` 等核心基础设施。
- 遵循仓库既有风格（record 配置、`ConfigureJsonConfig`、`CommandHandler`、消息扩展）。

## 实施步骤

### 步骤 1：新增插件工程

`src/plugins/ZeroBot.Synthesize/ZeroBot.Synthesize.csproj`：

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
</Project>
```

### 步骤 2：配置模型 `SynthesizeOptions.cs`

- `Endpoint`：默认取环境变量 `Z_VTUBER_TRAINING_ENDPOINT`，否则 `http://z-vtuber-training.vtuber.svc.cluster.local:8080`。
- `DailyLimit`（默认 3）、`Lang`（默认 `ZH`）。
- `DatasetAliases`：`Dictionary<string,string>`。
- `DailyQuotas`：`Dictionary<string, DailyQuota>`，`DailyQuota(string Date, int Count)`。
- `Default` 静态属性供 `ConfigureJsonConfig` 使用。

### 步骤 3：额度工具 `SynthesizeQuota.cs`

纯函数，便于单测：

- `ChinaOffset = TimeSpan.FromHours(8)`。
- `Today(DateTimeOffset now)` → `DateOnly`（按 UTC+8）。
- `Key(long peerId, long senderId)` → `"{peerId}:{senderId}"`。

### 步骤 4：HTTP 客户端 `SynthesizeApi.cs`

- 注入 `HttpClient`、`ILogger<SynthesizeApi>`。
- `Task<byte[]?> SynthesizeAsync(string endpoint, string datasetId, string text, string lang, CancellationToken ct)`：
  - 拼接 URL 并对 datasetId 做 `Uri.EscapeDataString`；
  - `PostAsJsonAsync(url, new { text, lang })`；
  - 非成功状态码或空响应体返回 `null`；
  - 捕获异常记录日志并返回 `null`。

### 步骤 5：别名管理 `DatasetAliasCommandHandler.cs`

- `PredicateAsync`：`StartsWith("/synthesize")` 且 `IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "synthesize.alias")`。
- `HandleAsync`：解析参数兼容两种格式；参数不合法回复帮助；合法则经 `BeginConfigMutationScopeAsync` 写入 `DatasetAliases` 并 `SaveAsync`，用 `message.Reply(...)` 回复成功。
- 帮助文本：

```
/synthesize:dataset:{dataset-id}:{alias}
```

### 步骤 6：合成指令 `SynthesizeCommandHandler.cs`

- `PredicateAsync`：`message.Scene == MessageScene.Group` 且 `ToText().Trim().StartsWith("/学")`。
- `HandleAsync`：
  1. 原始文本解析出 `alias`、`text`；缺任一则回复帮助并返回。
  2. 从配置读取 `DatasetAliases`，无别名映射直接 `return`（不处理）。
  3. 原子检查并自增额度；超限回复「今日次数已用完」。
  4. 调 `SynthesizeApi`；成功则发送 `RecordOutgoingSegment`；失败则回滚额度并回复失败提示。

### 步骤 7：插件入口 `SynthesizePlugin.cs`

```csharp
services.AddSingleton<HttpClient>();
services.ConfigureJsonConfig("synthesize-config.json", SynthesizeOptions.Default, cancellationToken);
services.AddSingleton<SynthesizeApi>();
services.AddSingletonComponent<DatasetAliasCommandHandler>();
services.AddSingletonComponent<SynthesizeCommandHandler>();
```

### 步骤 8：接入 Core

- `src/ZeroBot.Core/ZeroBot.Core.csproj` 增加 ProjectReference。
- `src/ZeroBot.Core/Program.cs` 增加 `using ZeroBot.Synthesize;` 与 `TypedPluginLoader.Register<SynthesizePlugin>();`。
- `src/ZeroBot.Core/Dockerfile` 增加 `COPY ["src/plugins/ZeroBot.Synthesize/ZeroBot.Synthesize.csproj", "src/plugins/ZeroBot.Synthesize/"]`。
- `ZeroBot.slnx` 加入插件工程。

### 步骤 9：文档

- `src/plugins/AGENTS.md` 现有插件列表补充 `ZeroBot.Synthesize` 条目（功能、组件、命令、配置文件）。

### 步骤 10：单元测试

`test/ZeroBot.Core.Test/SynthesizeQuotaTest.cs`：

- UTC+8 与 UTC 跨日边界：`2026-09-30T16:00:00Z` → `2026-10-01`（北京时间 0 点）。
- `Key` 组合正确。

## 验证

```bash
dotnet build ZeroBot.slnx -c Release
dotnet test test/ZeroBot.Core.Test/ZeroBot.Core.Test.csproj -c Release
```

要求编译通过、测试通过。部署验收（合入发布流程后）：

| # | 场景 | 预期 |
|---|------|------|
| 1 | 高权限用户 `/synthesize:dataset:{id}:小松绿` | 回复别名绑定成功 |
| 2 | 普通用户执行别名指令 | 权限不足，无绑定 |
| 3 | 群聊 `/学:小松绿:你好` | 返回合成语音 |
| 4 | 私聊 `/学:小松绿:你好` | 无响应 |
| 5 | 同群同人第 4 次 `/学` | 提示次数用尽 |
| 6 | 修改配置文件 `endpoint` | 无需重启即生效 |

## 风险与回滚

- **风险**：接口返回格式假设（见技术提案）。已用失败保护避免误发。
- **回滚**：移除 Core 注册/引用与工程目录，或 `git revert` 后重新 publish；无数据迁移，`synthesize-config.json` 可直接删除。

## 交付清单

| 项 | 内容 |
|----|------|
| 分支 | `feat/synthesize-plugin` |
| 代码 | 新增 `ZeroBot.Synthesize` 工程（6 个源文件）+ Core/slnx 接入 + AGENTS.md |
| 测试 | `test/ZeroBot.Core.Test/SynthesizeQuotaTest.cs` |
| 文档 | `docs/plans/synthesize-voice-tech.md`、`docs/plans/synthesize-voice-impl.md` |
