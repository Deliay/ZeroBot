# 技术提案：开播通知与 B 站动态推送优化

- 日期：2026-09-04
- 状态：待评审
- 关联文档：[bilibili-live-dynamic-notify-optimize-prd.md](bilibili-live-dynamic-notify-optimize-prd.md)（产品需求）

## 现状分析

涉及两个文件，均在 `src/plugins/ZeroBot.Bilibili/`：

### 开播通知（`Live/LiveStatusSubscriber.cs`）

当前构建了 `image` 变量但从未使用，且构建方式有误：

```csharp
var image = info.UserCover is { Length: > 0 }
    ? Enumerable.Repeat(await info.UserCover.ToMilkyNonLocalImageSegmentAsync(cancellationToken), 0)
    : [];
// ... image 未传入 WriteManyGroupMessageAsync
```

`Enumerable.Repeat(x, 0)` 产出空序列，且整个变量没有进入消息段列表，封面图从未实际发送。

### 动态推送（`Dynamic/DynamicMessageBuilder.cs`、`Dynamic/VtuberSpaceApi.cs`）

- `DynamicMessageBuilder.Build(DynamicData)` 只渲染正文/图片/转发分隔，没有发布人段落；
- DTO `DynamicModules` 仅映射了 `module_dynamic`，未映射 `module_author`（含作者昵称 `name`）；
- DTO `DynamicData` 未映射动态 ID 字段（B 站动态 item 顶层 `id_str`），转发场景下无法拼接 `https://www.bilibili.com/opus/{dynamicId}` 兜底地址；
- 转发的原动态链接在 `AppendDynamic` 递归中与普通动态走同一逻辑，无前缀标识。

## 实现方案

### 1. `LiveStatusSubscriber.cs`：封面图入列

将 `image` 类型改为 `OutgoingSegment[]`（单元素或空数组），并在 `WriteManyGroupMessageAsync` 的消息段列表末尾展开：

```csharp
OutgoingSegment[] image = info.UserCover is { Length: > 0 }
    ? [await info.UserCover.ToMilkyNonLocalImageSegmentAsync(cancellationToken)]
    : [];
// ...
await bot.WriteManyGroupMessageAsync(accountId, [targetGroup], cancellationToken,
[
    ..atAll,
    $"{status}啦~\n{info.Title} {url}\n{duration}".ToMilkyTextSegment(),
    ..image,
]);
```

要点：

- 用集合表达式替代 `Enumerable.Repeat(..., 0)`，不再需要 LINQ 包装；
- `ToMilkyNonLocalImageSegmentAsync` 返回 `ImageOutgoingSegment`（继承 `OutgoingSegment`），协变数组转换 `ImageOutgoingSegment[]` → `OutgoingSegment[]` 合法；直接声明为 `OutgoingSegment[]` 更直观；
- 封面图对开播/下播通知均生效（与现有 `image` 构建位置一致，不区分方向）。

### 2. `VtuberSpaceApi.cs`：补充 DTO 映射

```csharp
public class DynamicData
{
    [JsonPropertyName("id_str")] public string IdStr { get; set; } = "";
    // type / modules / orig 不变
}

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

后台 vtuber server 透传 B 站动态 item 原始 JSON，`module_author.name` 与 `id_str` 均为 B 站动态 API 标准字段；字段缺失时反序列化为 `null`/空串，由消费方兜底。

### 3. `DynamicMessageBuilder.cs`：发布人段落与转发链接

**Build 签名增加发布人兜底参数**，首段固定输出：

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

调用方 `DynamicSubscriber` 传入订阅的 mid 作为兜底：`DynamicMessageBuilder.Build(item.Data, mid)`。

**转发链接处理**：抽出地址解析辅助方法，并给 `AppendDynamic` 增加「是否原动态」标识：

```csharp
private static string? GetDynamicUrl(DynamicData data)
{
    var link = NormalizeUrl(data.Modules?.ModuleDynamic?.Major?.Opus?.JumpUrl);
    if (link != null) return link;
    return data.IdStr is { Length: > 0 } ? $"https://www.bilibili.com/opus/{data.IdStr}" : null;
}

private static void AppendDynamic(DynamicData data, List<OutgoingSegment> segments, bool isOrig = false)
```

- 转发动态自身（`isOrig == false` 且 `Type == ForwardType`）：渲染 desc 转发语后，追加 `GetDynamicUrl(data)` 得到的转发动态自身地址（无 `jump_url` 时走 opus 拼接兜底）；
- 原动态（递归调用传 `isOrig: true`）：opus 链接行前加前缀 `原动态：`（前缀放文本内，复用现有 `text.Append('\n').Append(link)` 的拼法，改为 `text.Append("\n原动态：").Append(link)`）；
- 非转发动态：`isOrig == false` 且非 ForwardType，链接逻辑与现状完全一致。

`LiveRcmdType` 分支保持不变（`DynamicSubscriber` 已在推送前跳过该类型）。

## 影响面

| 文件 | 动作 |
|------|------|
| `src/plugins/ZeroBot.Bilibili/Live/LiveStatusSubscriber.cs` | 改：`image` 类型与构建方式，消息段列表末尾追加封面图 |
| `src/plugins/ZeroBot.Bilibili/Dynamic/VtuberSpaceApi.cs` | 改：DTO 增加 `DynamicData.IdStr`、`DynamicModules.ModuleAuthor`、`ModuleAuthor` |
| `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicMessageBuilder.cs` | 改：发布人首段、转发双链接与「原动态：」前缀 |
| `src/plugins/ZeroBot.Bilibili/Dynamic/DynamicSubscriber.cs` | 改：`Build` 调用传入 mid 兜底 |

无配置变更、无存储迁移、不影响其他插件。

## 兼容性

- `DynamicMessageBuilder.Build` 的 `fallbackAuthor` 为可选参数，若有其他调用方（当前仅 `DynamicSubscriber`）不受影响；
- DTO 新增字段为纯增量映射，旧数据/旧后台返回缺字段时按兜底逻辑工作；
- 开播通知仅多发一个图片段，文本内容与 @全体 行为不变。

## 验证

1. `dotnet build src/plugins/ZeroBot.Bilibili/ZeroBot.Bilibili.csproj -c Release` 编译通过；
2. 单测/本地构造 `DynamicData`（普通、转发、转发无 `jump_url`、缺 `module_author` 四种样例）验证 `Build` 输出段落与链接格式；
3. 部署后观察订阅直播间开播/下播与订阅用户发动态/转发时的群内实际消息。
