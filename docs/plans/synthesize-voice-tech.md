# 技术提案：语音合成插件（ZeroBot.Synthesize）

- 日期：2026-10-01
- 状态：待评审
- 关联文档：[synthesize-voice-impl.md](synthesize-voice-impl.md)（实现方案）

## 背景

后台语音训练服务 `vtuber-training` 已提供数据集语音合成能力：

```
POST {endpoint}/api/training/datasets/{dataset-id}/synthesize
Content-Type: application/json

{ "text": "...", "lang": "ZH" }
```

响应体为合成后的音频（二进制）。用户希望在群聊中通过别名快速指定数据集并合成语音，同时限制滥用。

## 需求

| # | 需求 |
|---|------|
| R1 | 数据集别名管理指令：`/synthesize:{dataset-id}:{alias}`（兼容 `/synthesize:dataset:{dataset-id}:{alias}`，与用户示例一致）；仅高权限用户（sudoers / 群管理员 / 拥有 `synthesize.alias` 权限者）可用 |
| R2 | 合成指令：`/学:{alias}:{text}`；通过别名解析 `dataset-id`，解析不到则不处理；命中则调用合成接口，把音频发送到群聊 |
| R3 | 合成接口地址动态可配（JSON 配置，热加载） |
| R4 | 限流：同一群聊（PeerId）+ 同一发送人（SenderId）每天最多生成 3 条，刷新时间为 UTC+8 的 0 点 |
| R5 | 合成指令仅群聊可用，任何用户均可使用 |

## 现状分析

项目基于 EmberFramework 的插件化架构，已有可复用的模式：

- `IPlugin` + `IServiceCollection` 注册组件、热加载配置（`ConfigureJsonConfig`）。
- `CommandHandler` / `CommandQueuedHandler`：`PredicateAsync` 命中后由 `CommandDispatcher` 调用唯一的 `HandleAsync`。
- 权限：`IPermission` + `ChatPermissionExtensions`（`IsSudoerOrGroupAdminOrHasPermissionAsync`），与微博/B站订阅指令一致。
- HTTP：直接注入单例 `HttpClient`，参考 `ZeroBot.Weibo.WeiboApi`。
- 消息片段：`Milky.Net.Model` 提供 `RecordOutgoingSegment`，与 `ImageOutgoingSegment` 一样支持 `base64://`、`file://`、`http(s)://`。
- 配置持久化：`IJsonConfig<T>` 的 `BeginConfigMutationScopeAsync` + `SaveAsync`（参考 `LiveStatutCommandHandler`、`Permission`）。
- `CommandDispatcher` 对一条消息只命中一个 handler（`FirstOrDefault`），因此两个指令的 `PredicateAsync` 必须互斥。

## 方案设计

### 新增插件工程 `ZeroBot.Synthesize`

```
src/plugins/ZeroBot.Synthesize/
├── ZeroBot.Synthesize.csproj
├── SynthesizePlugin.cs              # IPlugin，注册配置/组件/服务
├── SynthesizeOptions.cs             # 热加载配置模型
├── SynthesizeApi.cs                 # 合成接口 HTTP 客户端
├── SynthesizeQuota.cs               # 每日额度与日期工具（纯函数，可测）
├── DatasetAliasCommandHandler.cs    # /synthesize:... 别名管理
└── SynthesizeCommandHandler.cs      # /学:... 合成
```

依赖仅 `ZeroBot.Abstraction` 与 `ZeroBot.Utility`（`Milky.Net.Model` 经 Abstraction 传递）。

### 配置模型（`synthesize-config.json`）

```jsonc
{
  "endpoint": "http://z-vtuber-training.vtuber.svc.cluster.local:8080",
  "dailyLimit": 3,
  "lang": "ZH",
  "datasetAliases": { "小松绿": "6aba45e18826119a12d738a4" },
  "dailyQuotas": { "123456:7890": { "date": "2026-10-01", "count": 2 } }
}
```

- `Endpoint` 支持环境变量 `Z_VTUBER_TRAINING_ENDPOINT` 作为默认值，配置文件存在后以文件为准；文件热加载，修改后无需重启（R3）。
- `DatasetAliases`：全局别名 → dataset-id 映射（别名为全局语义，不区分群）。
- `DailyQuotas`：`{peerId}:{senderId}` → `{date, count}`，持久化以跨越重启，保证「每天最多 3 条」。
- `DailyLimit` / `Lang` 均配置化，便于运营调整。

### 权限与路由

| 指令 | Predicate 条件 | 处理 |
|------|----------------|------|
| `/synthesize:...` | `StartsWith("/synthesize")` 且 `IsSudoerOrGroupAdminOrHasPermissionAsync(bot, message, "synthesize.alias")` | 解析参数，写入别名，回复结果 |
| `/学:...` | 群聊 且 `StartsWith("/学")` | 解析别名与文本，限流，调用接口，发送 `RecordOutgoingSegment` |

- 两个 predicate 前缀不同，不会互相抢占。
- `/学` 在私聊直接 `return false`，`CommandDispatcher` 不会分发，实现「仅群聊」（R5）。

### `/synthesize:...` 参数解析

```
/synthesize:dataset:{dataset-id}:{alias}   -> ["dataset", "{dataset-id}", "{alias}"]
/synthesize:{dataset-id}:{alias}           -> ["{dataset-id}", "{alias}"]
```

优先识别首参为字面量 `dataset` 的形式（与用户示例一致），否则回退到两参形式；两者都兼容。

### `/学:...` 文本解析

`TextCommandParser` 会以 `:`、`：`、`-` 切分参数，会破坏含这些字符的文本。因此 `/学` 采用**原始文本解析**：定位 `/学` 之后、别名之后的首个分隔符，其余全部作为 `text` 原文，保留冒号、连字符等。

```
/学:小松绿:你好，世界-测试   -> alias="小松绿", text="你好，世界-测试"
```

### 限流流程（R4）

1. 计算日期：`DateTimeOffset.UtcNow.ToOffset(+8h)` 的日期（UTC+8 零点自然翻转，中国无夏令时）。
2. key = `{peerId}:{senderId}`。
3. 原子「检查并自增」：配置 mutation scope 内，若记录日期非今日则重置为 0；若 `count >= DailyLimit` 返回 false；否则 +1 并 `SaveAsync`。
4. 调用合成接口成功后保留计数；接口失败则回滚（-1），失败尝试不占用额度。
5. 超限时回复提示，不调用接口。

### HTTP 调用

```csharp
POST {Endpoint}/api/training/datasets/{Uri.EscapeDataString(datasetId)}/synthesize
{ "text": text, "lang": "ZH" }
```

- 用 `PostAsJsonAsync`（Web 默认 camelCase，序列化为 `text`/`lang`）。
- 非 2xx 或空响应体视为失败。
- 响应二进制经 `new RecordOutgoingSegment(new MilkyUri($"base64://{Convert.ToBase64String(bytes)}"))` 发送，与图片发送方式一致。

## 影响面

| 文件 | 动作 |
|------|------|
| `src/plugins/ZeroBot.Synthesize/**` | 新增插件工程与源码 |
| `src/ZeroBot.Core/ZeroBot.Core.csproj` | 新增 ProjectReference |
| `src/ZeroBot.Core/Program.cs` | 注册 `SynthesizePlugin` |
| `src/ZeroBot.Core/Dockerfile` | 新增插件 csproj 的 COPY（供容器构建 restore） |
| `ZeroBot.slnx` | 加入解决方案 |
| `src/plugins/AGENTS.md` | 补充插件说明 |
| `docs/plans/synthesize-voice-*.md` | 新增方案文档 |
| `test/ZeroBot.Core.Test/SynthesizeQuotaTest.cs` | 新增额度/日期单元测试 |

无数据库迁移、无其他插件改动；除新增 `synthesize-config.json` 外不改动任何部署配置。

## 风险

- **接口形态假设**：假设合成接口返回原始音频二进制。若实际返回 JSON（如 URL/base64 字段），需在 `SynthesizeApi` 增加解析分支。已通过「非 2xx/空体即失败」保证不会误发坏数据。
- **base64 体积**：长文本音频转 base64 体积较大，但与其他图片发送路径一致，可接受。
- **别名全局唯一**：同名别名会覆盖旧映射，属预期（高权限用户操作）。
- **额度持久化**：写配置文件在低并发下开销可忽略；跨重启仍能限流。
