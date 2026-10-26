# Launcher Hub

> 给 agent 用的 Windows 启动器模板：**一个壳 + 一份开发规范 + 一套 skill**。  
> agent 按规范把任意本地应用接进来——清单、图标、启动、健康探测、日志——不用写 UI 代码。

![主页明暗切换与页签切换](preview/demo.gif?v=2)

## 为什么是模板

本地服务越装越多，启动器也越来越碎：一个应用一个 exe、一套路径、一堆脚本。  
Launcher Hub 把启动器做成模板：框架只负责壳，接入信息全部落到清单和图标里。

| 给 agent 的输入 | 产出 |
|---|---|
| `docs/agent-guide.md`、`schemas/app.schema.json` | `apps/<id>/app.json` |
| `assets/placeholders/`（设计 brief + prompt + 生成脚本） | 1024 图标 / mark |
| `skills/windows-desktop-gdi-launcher`、`skills/responsive-container-audit` | 窗口、缩放、布局与审计的硬规则 |

## Agent 接入一个新应用

1. **建清单**：复制 `apps.example/<id>/` 到 `apps/<id>/`，或按 `schemas/app.schema.json` 新建；
2. **填行为**：`launch`（`process` / `powershell` / `http`）、`health`（`http` / `tcp` / `none`）、`port`、`log`、`params`、`accent`；
3. **接图标**：有官方资源就放本地 `assets/official/`，清单标 `source=official`；没有就用 `assets/placeholders/` 的原创包（或按 `DESIGN.md` + prompt 生成新的），清单标 `source=placeholder`；
4. **校验**：`tools/validate-apps.ps1` → 起 exe 跑 `--audit-layout` / `--audit-anim` / `--audit-icons`；
5. **交付**：`LauncherHub.exe` + `apps/` + `assets/`。

> `apps.example` 里是三种接入形态的样例（HTTP 探测、TCP 探测、日志解析）。只要 `apps/` 存在，运行时就不会使用内置示例。

## 能力

| 能力 | 说明 |
|---|---|
| 单文件 exe | Windows 10/11，`.NET Framework 4.8 / WinForms / GDI+`，零第三方运行时依赖 |
| 状态机 | 已停止 → 启动中 → 运行中 → 停止中；启动超时与异常原因可见 |
| 启动/停止 | 幂等启动、端口预检、停止杀进程树、外部实例接管 |
| 健康探测 | HTTP / TCP / none，每秒后台并行 |
| 日志 | tail + ANSI 清理，按应用解析时间/级别 |
| 窗口 | DWM 原生圆角 + 自绘标题栏、托盘、单实例、亮/暗主题、220 ms 页签过渡 |
| 缩放 | Per-Monitor V2 DPI + 窗口响应式，逻辑坐标布局，四档审计 |
| 清单 | `apps/<id>/app.json`；`kind: generic` 无需改 C# |

## 快速开始

```powershell
git clone https://github.com/ZYFsir/launcher-hub.git
cd launcher-hub
Copy-Item config.example.json config.local.json    # 填本机路径 / 远程入口
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\LauncherHub.exe
```

没有 `config.local.json` 也能启动：未配置的应用会给出明确错误。

## 给 agent 的文档

- [Agent 操作手册](docs/agent-guide.md)
- [App manifest schema](schemas/app.schema.json) · [schema 说明](schemas/README.md)
- [占位图标包](assets/placeholders/README.md) · [设计 brief](assets/placeholders/DESIGN.md)
- [SKILL：Windows 自绘启动器](skills/windows-desktop-gdi-launcher/SKILL.md)
- [SKILL：容器查询与防重叠](skills/responsive-container-audit/SKILL.md)
- [资产与品牌政策](docs/资产与品牌政策.md)

## 界面

| 亮色 | 暗色 |
|---|---|
| ![亮色](preview/home.png) | ![暗色](preview/dark-home.png) |

## License

- 代码：MIT（[LICENSE](LICENSE)）
- 官方品牌图标/字标不属于 MIT；授权链与风险见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 与 [资产与品牌政策](docs/资产与品牌政策.md)
- 原创占位图标：CC0-1.0（DSH 头像 CC BY-SA 4.0）
- 本项目不隶属于任何被集成的应用，也不代表官方背书
