# 动态图标来源

- 图标：useAnimations（<https://useanimations.com/zh>）
- 许可：MIT
- 采用图标：`home`、`settings`、`file-text`、`refresh-v2`、`maximize-2`、`moon`、`play-pause`、`minus`
- 窗口控制 `max` / `restore` / `close` 为与现有线框风格一致的项目自绘 sprite（缩放/旋转/脉冲），不是 useAnimations 素材；`minus` 来自 useAnimations。
- 处理：用官方 Lottie JSON 逐帧渲染成 64×64 白色 alpha 帧，横向拼接为 sprite sheet；运行时按文字颜色着色。动画交互由启动器实现（悬停播放一次、播放/暂停跟随服务状态）。

Animated icons by useAnimations (MIT).
