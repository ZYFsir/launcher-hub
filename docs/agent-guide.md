# Agent 操作手册：把 Launcher Hub 改成用户的启动器

> 这是给 agent 的约束与流程，不是最终用户手册。目标：**不重写框架，只换配置、图标和主题 token。**

## 1. 先读约束

- 窗口：native frame hybrid，DWM 负责圆角/阴影/1px 边框；不要用 `Region` 画外框。
- 页签：简化新拟态，左圆角/右方角，单一路径，无梯形/距离场/bridge/strip。
- 缩放：`RenderScale = DpiScale × WindowScale`；逻辑坐标布局；内容限宽。
- 控件：不得重叠；按卡片容器宽高做容器查询；跑 `--audit-layout`。
- 图标：`tile` 为完整 1024 方形图标，`mark` 为透明标记，`word` 可省略（界面用系统字体绘制名称）。

## 2. 新增一个应用

```text
apps/<id>/
  app.json
  icons/              # 用户本地可放官方图标（gitignore）
```

复制 `apps.example/<app>/app.json` 后修改：

- `id`：小写字母/数字/连字符；
- `name`：显示名称（文字可以出现官方名称，但不要默认打包官方 logo）；
- `kind`：`llama` / `comfy` / `dsh` / `generic`；
- `launch`：命令、参数、工作目录、停止策略；
- `health`：HTTP/TCP/无；
- `port`：默认端口；
- `params`：用户可改参数；
- `icon.tile`：完整图标路径；
- `icon.source`：`placeholder` / `official` / `user`。

## 3. 图标制作

- 先写设计 brief（概念/几何/颜色/安全区），再写具体图像提示词；
- 占位图标使用 `assets/placeholders/` 的原创包；
- 生成后检查：
  - 1024×1024；
  - 圆角/安全边距；
  - 亮/暗背景都清楚；
  - 不含文字/水印；
  - 与现有品牌轮廓有明显区别。
- 用户要官方图标时：
  - 只放用户本地 `assets/official/`；
  - 不提交到公开仓库；
  - 在 manifest 标 `source=official`；
  - 不暗示官方合作。

## 4. 改样式

- 改样式时先改 `Look.cs`/Renderer 中的 token；`themes/*.json` 目前是目标契约，尚未被运行时消费；
- 保持扁平、crisp text、无玻璃拟态/重阴影；
- 改字号/间距后，跑四档缩放截图 + `--audit-layout`；
- 不引入动画期间改布局的属性。

## 5. 验证

```powershell
.\LauncherHub.exe --audit-layout
.\LauncherHub.exe --audit-anim
.\LauncherHub.exe --audit-icons
.\LauncherHub.exe --selftest
```

验收报告必须包含：

- 1000/1240/1600/2560 真实窗口对比图；
- 最小宽度下按钮/说明不重叠；
- DPI 待测项明确标注。
