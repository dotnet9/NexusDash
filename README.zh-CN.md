# NexusDash

[English](README.md) | 简体中文

NexusDash 是一个 Avalonia 桌面仪表盘，用于本机系统监控。项目使用 `NexusDash.slnx`，并通过 `Directory.Packages.props` 启用 NuGet 中央包管理。

## 第三方开源组件审计（2026-05-20）

检查方式：`dotnet restore NexusDash.slnx`、`dotnet list src/NexusDash.csproj package --include-transitive`、NuGet `.nuspec`、NuGet.org 与源码仓库信息。优先接受 MIT / Apache-2.0 / BSD；LGPL-3.0 等其它开源协议在源码与传递依赖均可追溯时单独标注。

整改：

- 新增 `Directory.Packages.props`，直接依赖统一走中央包管理。
- `AtomUI.Desktop.Controls` 从 `5.1.5-build.11` 升级到 `6.0.0-build.3`，对齐当前 Zhijian 基线。
- `Avalonia` / `Avalonia.Desktop` 从 `11.3.12` 升级到 `12.0.3`。
- 显式添加 `ReactiveUI.Avalonia` `12.0.1`，因为启动代码直接调用了它的 Avalonia 集成 API。
- 移除 `Avalonia.Diagnostics`；该包可用版本仍停留在 11.x，不适合 Avalonia 12 基线。
- `System.Diagnostics.PerformanceCounter` / `System.Management` 保持最新稳定版 `10.0.8`；更高的 11.x 版本仍是 preview。
- `Tmds.DBus.Protocol` pin 到 `0.93.0`，对齐已审计的 Zhijian 基线。

| 包 | 使用范围 | 协议 | 源码/项目地址 | 结论 |
| --- | --- | --- | --- | --- |
| `AtomUI.Desktop.Controls` `6.0.0-build.3` | 桌面控件 | LGPL-3.0 | https://github.com/AtomUI/AtomUI | NuGet 包指向公开源码；按“源码与传递依赖可追溯”通过 |
| `Avalonia` / `Avalonia.Desktop` | 桌面运行时 | MIT | https://github.com/AvaloniaUI/Avalonia | 通过，已升级到 `12.0.3` |
| `ReactiveUI.Avalonia` `12.0.1` | Avalonia ReactiveUI 集成 | MIT | https://github.com/reactiveui/reactiveui | 通过，为应用启动集成显式声明直接引用 |
| `System.Diagnostics.PerformanceCounter` / `System.Management` | Windows 系统指标 | MIT | https://github.com/dotnet/dotnet | 通过，稳定版 `10.0.8` |
| `Tmds.DBus.Protocol` | Avalonia Linux DBus 传递依赖 | MIT | https://github.com/tmds/Tmds.DBus | 通过，pin 到 `0.93.0` |

传递依赖检查结论：AtomUI 链路中的 `AtomUI.Core`、`AtomUI.Controls.Shared`、`AtomUI.Fonts.AlibabaSans`、`AtomUI.Icons.AntDesign`、`AtomUI.Native` 均来自 https://github.com/AtomUI/AtomUI，源码开放；Avalonia / SkiaSharp / ANGLE、ReactiveUI / Splat、Svg.Controls.Avalonia / Svg.*、ExCSS、DynamicData、HarfBuzzSharp、MicroCom.Runtime 均有公开源码。有效 restore 未发现 `AvaloniaUI.DiagnosticsSupport`、`Semi.Avalonia.*` 黑盒扩展或已知高危包告警。
