---
name: windows-desktop-gdi-launcher
description: Build, refactor, or review a Windows-only self-drawn desktop launcher (WinForms + GDI+ + native DWM frame). Use for desktop UI scaling, DPI/responsive layout, borderless rounded windows, custom title bars, neumorphic tabs, sprite animation audits, layout-overlap tests, or when debugging blurry/clipped/stretched WinForms UIs. Not for Electron/Tauri/Web UIs.
license: MIT
---

# Windows 桌面自绘启动器

> 来源：`launcher-hub` 2026-10 的实际开发经验（v2.4）。
> 目标：零依赖、单 exe、Windows-only、60fps、可做精细视觉。

## 0. 什么时候用 / 不用

**用**：

- WinForms 自绘 UI，或需要替换系统标题栏；
- 窗口缩放/DPI/多屏下出现裁切、重叠、模糊、字小；
- 圆角窗口 + 1px 边框做不干净；
- 页签/卡片新拟态反复失败；
- 需要给桌面 UI 加动画审计、布局重叠审计。

**不用**：

- Web/Electron/Tauri 应用，直接用 CSS 容器查询；
- 需要跨平台；
- 需要原生控件无障碍/表格/复杂文本编辑为主的应用，优先 WinUI/WPF。

## 1. 默认技术栈

```text
Windows 11
C# 5 / .NET Framework 4.8
WinForms 只做宿主
GDI+ / System.Drawing 全自绘
PowerShell 5.1 + csc.exe 构建
零第三方运行时依赖
单 exe + assets
```

渲染链路：

```text
OnPaint
  → 物理像素 Bitmap (32bppRgb)
  → Graphics.ScaleTransform(RenderScale)
  → Renderer.Render(logical size)
  → DrawImageUnscaled
```

**不要**把设计稿先渲染成小图再整体放大；文字会糊。应在物理帧上用矢量 transform 绘制。

## 2. 窗口：native frame hybrid

无边框 + 自绘标题栏不要用 `FormBorderStyle.None` + `Region`。

推荐：

```csharp
FormBorderStyle = FormBorderStyle.Sizable;
AutoScaleMode = AutoScaleMode.None;
// 保留 native frame，让 DWM 画圆角/阴影/1px 边框/缩放
// WM_NCCALCSIZE 返回 0，把 caption 高度并入客户区
// WM_NCHITTEST 返回 HTCLIENT，交给自定义 hit-test
```

规则：

- DWM 负责外圈；应用只画客户区；
- 普通窗口圆角、最大化方形；set `DWMWCP_ROUND` / `DONOTROUND`；
- 不要自绘 1px 圆角外框再去裁 Region，会出现锯齿/断线/粗细不一；
- 如果必须完全自绘 1px AA 圆角边框，只有 **layered window + per-pixel alpha** 一条路；
- 客户区尺寸用 outer `Size` 设定，注意 native frame 会加非客户区；
- DWM 默认边框通常比 `DWMWA_BORDER_COLOR` 干净；自定颜色在圆角处可能断裂；
- 保留 `WS_THICKFRAME`/Snap/最大化语义。

## 3. DPI + 响应式（三个独立维度）

```text
DpiScale     = DeviceDpi / 96
WindowScale  = clamp(min((clientW / DpiScale) / 1240,
                          (clientH / DpiScale) / 800), 0.78, 1.25)
RenderScale  = DpiScale × WindowScale
```

实现：

- 进程使用 **Per-Monitor V2**：`SetProcessDpiAwarenessContext(-4)`，失败回退 `SetProcessDPIAware`；
- 布局用逻辑坐标：`logical = physical / RenderScale`；
- 渲染时 `ScaleTransform(RenderScale)`，字体/图标/间距/圆角/按钮统一缩放；
- 鼠标/命中测试 `ToLogical`，内联编辑框 `ToPhysical`；
- 处理 `WM_DPICHANGED`，重建布局/字体/缓存；
- 设计最小尺寸按逻辑单位，物理尺寸 = 逻辑 × DpiScale；
- 内容平面最大宽度（如 1380），超宽居中，不无限拉卡片；
- 小窗允许缩到 0.78，优先保证不裁切；
- 不要关掉 DPI 感知靠系统位图拉伸。

**响应式 = 重排，不是等比放大整个画布**。
窗口断点只决定列数/内容宽度；卡片内部按自身容器宽高做“容器查询”。

## 4. 视觉系统（已锁定决策）

- 扁平、现代、crisp text、60fps；
- 亮/暗两套主题，无贴纸/手绘/玻璃拟态；
- 内部卡片圆角，窗口外圈 DWM；
- **选中页签用简化新拟态**：
  - 单一路径：左圆角、右方角；
  - 右缘停在 rail 右边框；
  - fill = `TabBase`（比 rail 暗约 5.5% Ink）；
  - 缓存深色内阴影（左上）+ 底部 1px 高光；
  - 页签与卡片间保留页面底色，不填 strip/bridge；
  - 不做梯形/距离场/轮廓插值/卡片凹口。
- 窗口控制图标画进正方形 dest；44×30 热区内居中 40×40，不拉伸 sprite；
- 选中/hover 用背景/字重变化，不在 hover 改 padding/font-size。

## 5. 动画与图标审计

- 所有可点击图标必须有 >1 帧 sprite；
- 审计不只看资产：要**离屏渲染真实布局**并记录实际调用的 `AnimIcon` 角色；
- 检查绘制 dest 宽高比 ±5%，避免 sprite 被压扁；
- 缓存 tinted sprite sheet，禁止每帧 ColorMatrix；
- 只动 transform/opacity；动画期间不动 width/height/padding；
- 首次绘制不播入场，切换页才播状态动画。

## 6. 布局/控件重叠（容器查询）

桌面没有 CSS `@container`，用同样的思维：

```text
container = card rect
if (container.h < 336) hide optional note
else draw note above bottom action row
```

规则：

- 优先级：主按钮 > 状态 > 字段 > 目录 > note；
- 高度不足先隐藏可选说明，再重排按钮；
- 宽度不足重排字段/按钮，不要只缩字体；
- 所有矩形用同一逻辑坐标；
- 不只靠肉眼截图，跑布局审计。

本项目的 `--audit-layout` 覆盖：

```text
1000×700 / 1240×800 / 1600×1000 / 2560×1392
检查：字段 vs 状态、note vs 按钮、按钮 vs 齿轮、
      卡片越界、终端高度、首页列宽、卡片间距
```

容器查询清单见 `skills/responsive-container-audit/SKILL.md`。

## 7. 构建与验收命令

```powershell
# 先关旧实例
Get-Process LauncherHub -ErrorAction SilentlyContinue | Stop-Process -Force
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1

LauncherHub.exe --audit-layout
LauncherHub.exe --audit-anim
LauncherHub.exe --audit-icons
LauncherHub.exe --audit-switch
```

收尾必须确认：

- exe 时间戳晚于源码；
- 当前进程已重启；
- 日志 PASS；
- 设计尺寸/缩放矩阵无裁切/重叠；
- 高 DPI 多屏待测项要明说。

## 8. 踩坑清单

- `UiScale()` 返回 1 = 没有 UI 缩放；
- 只缩放字体、不缩放几何 → 裁切；
- 只拉伸卡片 → 比例失衡；
- 缩放最终位图 → 文字糊；
- 鼠标坐标不换算 → 看得见点不中；
- System DPI Aware 在混合 DPI 多屏不可靠；
- `Region` 做圆角外框 → 1px 线被 1-bit 裁成锯齿；
- 自绘圆角外框 + DWM 圆角 → 两套曲率不一致；
- 页签/卡片用距离场或梯形渐变 → 接缝永远圆/裂；
- 页签加 bridge 到卡片层 → 出现竖条/补丁；
- 卡片固定 y 坐标 + 小高度 → note/按钮重叠；
- 动画审计只查资产不查接线 → 假 PASS；
- 页面截图忘记关旧进程 → 测的是旧 exe。

## 9. 项目地图

```text
src/HubForm.cs       窗口/DPI/输入/托盘
src/Renderer.cs      全部界面绘制
src/Paint.cs         图元/阴影/字体
src/LayoutAudit.cs   布局重叠审计
src/AnimationAudit.cs 动画覆盖审计
docs/                经验、调研、审计日志
skills/              容器查询等子 skill
```

参考项目：本仓库，当前 v2.4。

## 10. 验收清单

- [ ] Per-Monitor V2 + WM_DPICHANGED；
- [ ] RenderScale 三轴分离；
- [ ] 逻辑/物理坐标统一；
- [ ] 内容最大宽度，超宽居中；
- [ ] 1000/1240/1600/2560 + 100/125/150% 矩阵；
- [ ] layout/anim/icons/switch 审计 PASS；
- [ ] native frame 圆角/方角/边框正常；
- [ ] 页签简单新拟态，无 bridge/梯形；
- [ ] 图标正方形 dest，无压扁；
- [ ] 无控件重叠、无裁切、无旧进程；
- [ ] 验收报告包含 1000/1240/1600/2560 真实窗口缩放对比图，并注明 DPI 待测项。
