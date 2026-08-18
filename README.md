# Float Menu Performance Optimizer

A RimWorld 1.6 performance mod that controls how often map float-menu options are regenerated while preserving click-time validity checks.

## Features

- **Off:** Uses RimWorld's vanilla behavior and regenerates the menu every 4 rendered frames.
- **Frame mode:** Regenerates after a configurable number of rendered frames. The default is 30 frames.
- **Time mode:** Regenerates after a configurable real-time interval. The default is 0.5 seconds, with a minimum spacing of 4 rendered frames.
- **Manual mode:** Keeps the opening snapshot and regenerates only when an option is clicked.
- Every mode except Off immediately validates a clicked option against the current game state. Invalid actions are disabled without closing the menu.
- Optional rejection messages for failed click validation.
- English, Simplified Chinese, Traditional Chinese, Russian, Japanese, and Korean user interfaces.
- Includes targeted fixes for MoeLotl: Rigor Mortis, Milira Race, and Milira: Wings of Democracy.
- Targeted fix switches appear only when the corresponding mod is active, are enabled by default, and normally apply immediately.

## Requirements

- RimWorld 1.6
- Harmony

Load Harmony and affected target mods before this mod. No special bottom-of-list placement is required.

## Installation

Copy the complete `FloatMenuPerformanceOptimizer` folder into RimWorld's `Mods` directory, then enable Harmony and this mod.

## Building

The project is in `Source/FloatMenuRevalidationControl.csproj`. Its assembly references currently point to a local RimWorld installation and Harmony Workshop directory; adjust the `HintPath` values for your environment before building.

The compiled assembly is included in `Assemblies`.

## License

MIT

---

# 右键菜单性能优化

这是一个适用于 RimWorld 1.6 的性能优化 Mod，用于控制地图右键菜单选项在菜单开启期间的完整重新生成频率，同时保留点击时的有效性检查。

## 功能

- **关闭：** 使用 RimWorld 原版行为，每 4 个渲染帧完整生成一次菜单。
- **帧模式：** 按照可配置的渲染帧间隔完整生成菜单，默认间隔为 30 帧。
- **时间模式：** 按照真实时间间隔完整生成菜单，默认间隔为 0.5 秒，并且至少等待 4 个渲染帧。
- **手动模式：** 保留菜单打开时的快照，仅在点击选项时重新生成。
- 除关闭外，所有模式都会在点击选项时立即按当前游戏状态验证。失效操作会变灰，菜单保持开启。
- 可以选择是否在点击验证失败时显示拒绝消息。
- 支持英语、简体中文、繁体中文、俄语、日语和韩语界面。
- 内置萌螈僵尸拓展、米莉拉和米莉拉：民主之翼的针对性右键菜单修复。
- 仅在启用对应目标 Mod 时显示修复开关；默认启用，正常情况下修改立即生效。

## 依赖

- RimWorld 1.6
- Harmony

将 Harmony 和对应目标 Mod 排在本 Mod 前面即可，不需要刻意放在 Mod 列表底部。

## 安装

将完整的 `FloatMenuPerformanceOptimizer` 文件夹复制到 RimWorld 的 `Mods` 目录，然后启用 Harmony 和本 Mod。

## 编译

项目文件位于 `Source/FloatMenuRevalidationControl.csproj`。其中的程序集引用目前指向本地 RimWorld 和 Harmony 路径，在其他环境编译前需要修改相应的 `HintPath`。

编译后的程序集位于 `Assemblies`。

## 许可证

MIT
