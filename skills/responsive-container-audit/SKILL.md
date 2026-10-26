---
name: responsive-container-audit
description: Use container-query thinking in desktop self-drawn UIs (WinForms/GDI+, Qt, WPF custom paint) to make cards reflow by their own container width/height and to audit/avoid control and text overlap. Trigger when resizing causes clipped text, overlapping buttons, or when adding breakpoints/compact layouts.
license: MIT
---

# Skill: responsive-container-audit

**用途**：在桌面自绘 UI（WinForms/GDI+、Qt、WPF 自绘）里用“容器查询”思想做响应式，并用矩形/文本碰撞审计避免控件重叠。
**适用**：当前 Launcher Hub 的 `Geo.Right`、`Geo.Left`、`Geo.HomeCard` 等卡片容器。

## 核心概念

CSS 的 `@container` 不是“页面级媒体查询”的替代品，而是：

> 组件按**自己所在容器**的可用宽高改变布局，而不是按窗口宽度猜。

桌面自绘没有 CSS，但可以用同样的模型：

```text
container = card rect (w,h)
query     = if (container.w < 620) compact else regular
            if (container.h < 336) hide optional note
```

不要写：

```csharp
if (ClientSize.Width < 1100) ...
```

这会让卡片在不同列宽/不同容器里表现不一致。

## 本项目的容器层级

```text
Window (logical size)
  ├─ Rail (fixed width)
  ├─ Banner (full width)
  └─ Content plane (max 1380, centered)
       ├─ Left card   (container: Geo.Left)
       ├─ Right card  (container: Geo.Right)
       └─ Terminal   (container: Geo.Term)
```

窗口断点只负责 Content/列宽；卡片内部布局必须查询 `Geo.Left/Right` 自身的宽高。

## 布局规则

1. **先定义容器的 intrinsic min size**：字段、按钮、文字测量后的最小宽高。
2. **容器宽度不足时重排**，不是把字体/按钮等比缩小：
   - 字段换行/堆叠；
   - 次要说明隐藏；
   - 按钮换到下一行。
3. **容器高度不足时隐藏可选内容**：
   - note / tip / secondary description 先隐藏；
   - 主操作和状态永远保留；
   - 不允许按钮覆盖文本。
4. **可选内容必须有明确优先级**：
   ```text
   primary action > status > fields > folder links > note
   ```
5. **所有矩形来自同一逻辑坐标**；不要混用物理像素和逻辑值。
6. **测试按容器尺寸，不只按窗口尺寸**。

## 防重叠审计清单

对每个测试尺寸：

- [ ] 所有按钮矩形互不相交；
- [ ] 按钮与状态/说明/字段矩形不相交；
- [ ] 文本 rect 在容器内，未被按钮覆盖；
- [ ] 底部操作行 `c.Bottom - 58 .. c.Bottom - 18` 与 note/folders 不重叠；
- [ ] 右侧字段 + 状态 pill 不超过 `c.Right - 18`；
- [ ] 左卡图片/状态/按钮在容器内；
- [ ] 终端高度 > 0，且与卡片间距 ≥ 16；
- [ ] 最小窗口 1000×700、1600×1000、2560×1392 都通过。

## 当前项目落地

- `DrawRightCard`：note 使用 `noteY = min(348, c.Bottom - 58 - 44 - 12)`，且 `c.Height >= 336` 才绘制；
- 这是最小的容器查询：**高度不足就移除可选 note**，避免与底部按钮重叠。
- 后续若要更完整：
  - `Geo.Right.Width < 620` 时把 IP/端口/状态堆叠；
  - `Geo.HomeCard` 按卡片宽度切 2/3 列；
  - 所有可选说明统一走 `OptionalNote` 容器查询。

## 反模式

- 只缩字体，不缩布局；
- 用固定 y 坐标写死 note/按钮；
- 用整个窗口宽度决定卡片内部布局；
- 隐藏内容但不保留主操作；
- 只靠肉眼截图，不做矩形/文本碰撞测试。

## 测试建议

- 加 `--audit-layout`：遍历矩阵，检查卡片容器预算与按钮/说明矩形；
- 加截图矩阵：1000×700、1240×800、1600×1000、2560×1392；
- CI/收尾必须跑：`--audit-layout` + `--audit-switch` + `--audit-icons` + `--audit-anim`。
