# ZeroBot

## 项目结构

```
ZeroBot/
├── src/
│   ├── ZeroBot.Abstraction/    # 抽象层：接口定义
│   ├── ZeroBot.Core/           # 核心层：启动入口、服务注册
│   ├── ZeroBot.Utility/        # 工具层：扩展方法、配置热加载
│   └── plugins/                # 插件目录
│       ├── ZeroBot.Endfield/   # 终末地游戏集成
│       ├── ZeroBot.AI/         # AI 对话系统
│       ├── ZeroBot.Bilibili/   # B 站集成
│       └── ...
├── test/
└── docs/
```

详细插件说明见 [src/plugins/AGENTS.md](src/plugins/AGENTS.md)

---

## 部署手册

### 构建

```bash
dotnet clean src/ZeroBot.Core/ZeroBot.Core.csproj -c Release && dotnet publish src/ZeroBot.Core/ZeroBot.Core.csproj -c Release
```

输出到 `src/ZeroBot.Core/bin/Release/net10.0/publish/`，**不得修改 `-o` 参数改变输出目录**。

### 发布后重启

```bash
pm2 restart ZeroBot --update-env
```

**严禁使用 `pm2 delete`**。

### 配置

- pm2 生态文件: `~/Projects/Bot/pm2/zero-bot.json`
- 应用工作目录: `~/Projects/Bot/ZeroBot`（配置文件目录）
- **不得修改任何配置文件**（pm2 生态文件、JSON 配置等），除非用户明确指示

### 日志

```bash
pm2 log ZeroBot --lines 30
```