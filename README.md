# Float Menu Performance Optimizer

A RimWorld 1.6 / 1.7 performance mod that controls how often map float-menu options are regenerated while preserving click-time validity checks.

New configurations and Reset Settings default to Time mode at 0.50 seconds. Updating preserves existing saved modes, intervals, and switches.

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

- RimWorld 1.6 or 1.7
- Harmony

Load Harmony and affected target mods before this mod. No special bottom-of-list placement is required.

## Installation

Copy the complete `FloatMenuPerformanceOptimizer` folder into RimWorld's `Mods` directory, then enable Harmony and this mod.

## Building

For RimWorld 1.6, use `Source/FloatMenuRevalidationControl.csproj` with modern MSBuild, .NET Framework 4.8.1, and x64 Release. Set `RimWorld16DependenciesDir` to a preserved 1.6 reference snapshot containing `Managed/` and `Harmony/`; do not replace these references with 1.7 assemblies.

For RimWorld 1.7, use `Source/FloatMenuRevalidationControl.1.7.csproj` with the .NET 10 SDK and x64 Release. Set `RimWorldDir` to the 1.7 installation and `HarmonyDir` to its compatible Harmony assembly directory.

Compiled assemblies are included separately in `1.6/Assemblies` and `1.7/Assemblies`. `LoadFolders.xml` selects only the matching version. Redirect build and intermediate outputs to staging for compile-only checks; exit the game before replacing active DLLs.

The final 1.7 build passed manual gameplay checks. The updated 1.6 defaults passed compilation, assembly-reference checks, and 15 offline settings checks; the new 1.6 DLL has not yet been retested in the 1.6 game.

## License

MIT

---

# 右键菜单性能优化

这是一个适用于 RimWorld 1.6 / 1.7 的性能优化 Mod，用于控制地图右键菜单选项在菜单开启期间的完整重新生成频率，同时保留点击时的有效性检查。

新配置及“重置设置”默认使用时间模式，间隔 0.50 秒。更新会保留已有配置的模式、间隔与开关。

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

- RimWorld 1.6 或 1.7
- Harmony

将 Harmony 和对应目标 Mod 排在本 Mod 前面即可，不需要刻意放在 Mod 列表底部。

## 安装

将完整的 `FloatMenuPerformanceOptimizer` 文件夹复制到 RimWorld 的 `Mods` 目录，然后启用 Harmony 和本 Mod。

## 编译

RimWorld 1.6 使用 `Source/FloatMenuRevalidationControl.csproj`，以现代 MSBuild 编译 .NET Framework 4.8.1 / x64 Release。将 `RimWorld16DependenciesDir` 指向包含 `Managed/` 和 `Harmony/` 的固定 1.6 引用快照，不可混用 1.7 程序集。

RimWorld 1.7 使用 `Source/FloatMenuRevalidationControl.1.7.csproj`，以 .NET 10 SDK 编译 x64 Release；通过 `RimWorldDir` 和 `HarmonyDir` 指定 1.7 游戏及兼容 Harmony 目录。

两版程序集分别位于 `1.6/Assemblies` 和 `1.7/Assemblies`，由 `LoadFolders.xml` 只加载匹配版本。仅编译检查时应将输出与中间文件重定向到 staging；替换活动 DLL 前须退出游戏。

最终 1.7 构建已通过游戏内手测。1.6 默认值补编已通过编译、程序集引用及 15 项离线设置检查，新 1.6 DLL 尚未在 1.6 游戏中重新验收。

## 许可证

MIT
