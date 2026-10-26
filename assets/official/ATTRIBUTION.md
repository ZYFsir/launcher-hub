# 品牌资产来源与许可

本目录下的文件均来自项目官方渠道，未使用手绘草稿描摹。`assets/*.png` 只是把官方 SVG 按运行时需要做透明底、单色化、缩放；未重绘、未改变轮廓。

## llama.cpp

- 来源：<https://github.com/ggml-org/llama.brand>（`logo/llama-cpp/logo-llama-cpp-dark.svg`、`icon/icon-light.svg`）
- 许可：CC BY-NC 4.0；`llama-BRAND-USAGE.md` 额外授予在软件分发/集成中为指代 Llama.cpp 而复制、缩放、单色化、格式转换的许可。
- 本项目的使用方式：在启动器里指代本机 llama.cpp 服务，未暗示官方背书。
- 派生文件：`llama_word.png`（官方深色完整字标栅格化）、`llama_mark.png`（官方浅色图标栅格化）。

## ComfyUI / Comfy

- 来源：<https://comfy.org/zh-CN/brand> 的品牌资产（`https://comfy.org/icons/logo.svg`、`https://comfy.org/icons/comfyicon.svg`）
- 商标：Comfy 和 ComfyUI 是 Comfy Org 的商标；本启动器只在准确描述“本机 ComfyUI 服务”的语境中使用，不暗示官方合作。
- 本项目的使用方式：未改形状/配色，只做透明底栅格化与缩放。
- 派生文件：`comfy_word.png`（官方 `logo.svg`）、`comfy_mark.png`（官方 `comfyicon.svg`）。

## DeepSeek Harness

- 来源：本机官方 DSH Web 客户端 `@deepseek-ai/dsh-client-ui-primitives` 的 `FishLogo` / `BrandWordmark`（官方侧栏品牌 slot 渲染的 SVG），以及 `dsh-web-frontend/dist/favicon.svg`。
- 本项目的使用方式：从官方运行中的侧栏 SVG 导出字标与鲸鱼标；只做颜色替换（深色/白色版本）和栅格化，未重绘轮廓。
- 派生文件：`dsh_word.png`（官方 “deepseek HARNESS” 字标）、`dsh_mark.png`（官方鲸鱼标，深色版；界面里用 Tinted 转白）。

## 动态图标（非品牌）

- 来源：<https://useanimations.com/zh>，选择的具体图标见 `assets/anim/` 的 sprite sheet 与 `manifest.txt`。
- 许可：MIT（站点声明）；无需署名，本项目在 README 中致谢。
- 生成方式：官方 Lottie JSON → lottie-web（canvas）逐帧渲染为 64×64 白色 alpha 帧 → 横向 sprite sheet；运行时用 `P.TintedFrame` 按当前文字颜色着色。
- 当前采用：`home`、`settings`、`file-text`、`refresh-v2`、`maximize-2`、`moon`、`play-pause`、`minus`。窗口最小化/最大化/关闭保留系统风格自绘（useAnimations 没有同形状的窗口控件），避免为了“全动态”硬套不匹配图标。
