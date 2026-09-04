# 实现方案：开播通知与 B 站动态推送优化

- 日期：2026-09-04
- 状态：待实施
- 分支：`feat/bilibili-live-dynamic-notify-optimize`（基于 origin/main）
- 关联文档：[bilibili-live-dynamic-notify-optimize-prd.md](bilibili-live-dynamic-notify-optimize-prd.md)（产品需求）、[bilibili-live-dynamic-notify-optimize-tech.md](bilibili-live-dynamic-notify-optimize-tech.md)（技术提案）

## 范围

改动 4 个文件，全部位于 `src/plugins/ZeroBot.Bilibili/`，无配置变更、无存储迁移、不涉及其他插件：

| # | 文件 | 动作 |
|---|------|------|
| 1 | `Live/LiveStatusSubscriber.cs` | 修复 `image` 构建方式并追加到消息末尾 |
| 2 | `Dynamic/VtuberSpaceApi.cs` | DTO 补充 `id_str`、`module_author` 映射 |
| 3 | `Dynamic/DynamicMessageBuilder.cs` | 发布人首段、转发双链接与「原动态：」前缀 |
| 4 | `Dynamic/DynamicSubscriber.cs` | `Build` 调用传入 mid 作为发布人兜底 |

## 实施步骤

### 步骤 1：开播通知携带封面图（R1）

文件：`src/plugins/ZeroBot.Bilibili/Live/LiveStatusSubscriber.cs`

1. 将第 53–55 行的 `image` 变量改为 `OutgoingSegment[]` 类型，用集合表达式替代 `Enumerable.Repeat(..., 0)`：

```csharp
OutgoingSegment[] image = info.UserCover is { Length: > 0 }
    ? [await info.UserCover.ToMilkyNonLocalImageSegmentAsync(cancellationToken)]
    : [];
```

2. 在第 62–66 行 `WriteManyGroupMessageAsync` 的消息段列表末尾展开 `..image`：

```csharp
await bot.WriteManyGroupMessageAsync(accountId, [targetGroup], cancellationToken,
[
    ..atAll,
    $"{status}啦~\n{info.Title} {url}\n{duration}".ToMilkyTextSegment(),
    ..image,
]);
```

要点：`ToMilkyNonLocalImageSegmentAsync` 返回 `ImageOutgoingSegment`（继承 `OutgoingSegment`，见 `src/ZeroBot.Utility/EventExtensions.cs:169`），数组协变合法；开播/下播均追加封面（与现有 `image` 构建位置一致）；`UserCover` 为空时为空数组，行为与现状一致。

### 步骤 2：DTO 补充字段映射（R2、R3 前置）

文件：`src/plugins/ZeroBot.Bilibili/Dynamic/VtuberSpaceApi.cs`

1. `DynamicData`（第 53–60 行）增加：

```csharp
[JsonPropertyName("id_str")] public string IdStr { get; set; } = "";
```

2. `DynamicModules`（第 62–65 行）增加 `module_author` 映射，并新增 `ModuleAuthor` 类：

```csharp
public class DynamicModules
{
    [JsonPropertyName("module_author")] public ModuleAuthor? ModuleAuthor { get; set; }
    [JsonPropertyName("module_dynamic")] public ModuleDynamic? ModuleDynamic { get; set; }
}

public class ModuleAuthor
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}
```

要点：纯增量映射，后台返回缺字段时反序列化为 `null`/空串，由消费方兜底。

### 步骤 3：动态消息构建改造（R2、R3）

文件：`src/plugins/ZeroBot.Bilibili/Dynamic/DynamicMessageBuilder.cs`

1. `Build` 增加可选参数 `fallbackAuthor`，首段固定输出发布人段落：

```csharp
public static OutgoingSegment[] Build(DynamicData data, string? fallbackAuthor = null)
{
    var author = data.Modules?.ModuleAuthor?.Name;
    if (string.IsNullOrWhiteSpace(author)) author = fallbackAuthor ?? "神秘人";
    var segments = new List<OutgoingSegment>
    {
        $"{author} 发布了新动态".ToMilkyTextSegment(),
    };
    AppendDynamic(data, segments);
    return [.. segments];
}
```

2. 新增地址解析辅助方法（放在 `NormalizeUrl` 附近）：

```csharp
private static string? GetDynamicUrl(DynamicData data)
{
    var link = NormalizeUrl(data.Modules?.ModuleDynamic?.Major?.Opus?.JumpUrl);
    if (link != null) return link;
    return data.IdStr is { Length: > 0 } ? $"https://www.bilibili.com/opus/{data.IdStr}" : null;
}
```

3. `AppendDynamic` 增加 `bool isOrig = false` 参数，调整链接逻辑：

- 转发动态自身（`!isOrig && data.Type == ForwardType`）：渲染 desc 转发语后，追加 `GetDynamicUrl(data)` 的地址（无 `jump_url` 时走 opus 拼接兜底）；注意保持「`text.Length == 0` 时回退」的现有判断不被破坏；
- 原动态（递归调用处 `AppendDynamic(data.Orig, segments, isOrig: true)`）：opus 链接行前加前缀 `原动态：`（现有 `text.Append('\n').Append(link)` 改为 `text.Append("\n原动态：").Append(link)`）；
- 非转发动态：`isOrig == false` 且非 `ForwardType`，链接逻辑与现状完全一致；
- `LiveRcmdType` 分支保持不变。

### 步骤 4：订阅器传入发布人兜底（R2）

文件：`src/plugins/ZeroBot.Bilibili/Dynamic/DynamicSubscriber.cs`

第 44 行调用改为传入订阅的 mid：

```csharp
var segments = DynamicMessageBuilder.Build(item.Data, mid);
```

## 验证

1. 编译：`dotnet build src/plugins/ZeroBot.Bilibili/ZeroBot.Bilibili.csproj -c Release` 通过；
2. 本地构造 `DynamicData` 样例验证 `Build` 输出（项目当前无 `DynamicMessageBuilder` 单测，可用临时控制台/调试方式验证）：
   - 普通图文动态：首段「{昵称} 发布了新动态」，链接无前缀；
   - 转发动态：首段发布人；含转发动态自身地址 + 带「原动态：」前缀的原动态地址，共两条；
   - 转发且无 `jump_url`：转发动态自身地址为 `https://www.bilibili.com/opus/{id_str}`；
   - 缺 `module_author`：首段发布人显示为传入的 mid；
3. 部署后观察订阅直播间开播/下播与订阅用户发动态/转发时的群内实际消息（验收标准见 PRD）。

## 非目标

- 不改动订阅/取消订阅指令交互与权限；
- 不改动 `DYNAMIC_TYPE_LIVE_RCMD` 的跳过策略；
- 不改动 `BilibiliOptions` 配置结构与轮询节奏；
- 不调整动态正文富文本渲染与图片排列顺序。
