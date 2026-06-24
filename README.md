# NexusDash

NexusDash 是一个 Avalonia 桌面仪表盘，用于本机系统监控。项目使用 `NexusDash.slnx`，并通过 `Directory.Packages.props` 启用 NuGet 中央包管理。

## 仓库规范

- 当前版本：`0.1.2`，版本号统一维护在根目录 `Directory.Build.props` 的 `<Version>` 节点。
- NuGet 包项目统一支持 `net8.0;net10.0`；Demo、App、测试与内部应用项目统一使用 `net11.0` / `net11.0-windows`。
- 根目录 `logo.svg`、`logo.png`、`logo.ico` 是唯一图标源，子工程只通过 MSBuild `Link` 引用，不维护图标副本。
- 运行时帮助、Markdown 示例、内置备忘录、设计说明等业务文档按功能保留；仓库级入口文档使用根目录 `README.md` 和 `UpdateLog.md`。

## 性能与 UI 刷新优化（2026-05-29）

本轮优化重点是降低 NexusDash 自身 CPU、内存和 UI 刷新开销，同时保持监控界面可用。

主要改动：

- 将实时指标曲线改为轻量 Avalonia 控件绘制，避免每次刷新生成图像，并移除应用中的 `ScottPlot.Avalonia` 依赖。
- 按数据成本拆分刷新节奏：轻量系统指标约每 2 秒刷新；进程快照、进程树、Treemap 和网络连接约每 6 秒刷新。
- Windows 进程采集改为 Toolhelp/native 快照与原生 IO counters，昂贵的静态元数据刷新延长到 2 分钟。
- 移除当前总览 UI 未使用的每次磁盘枚举和活动 TCP 连接计数。
- 增加进程图标解码缓存，并只对应用进程提取图标。
- 将第三方日志查看器替换为轻量内置操作日志，日志区域配色与刷新成本由 NexusDash 控制。
- 主窗口最小化时自动暂停监控刷新，恢复窗口时自动继续刷新。
- 将主题相关界面统一收敛到 `AppBackgroundBrush`、`PanelBackgroundBrush`、`PanelAltBackgroundBrush`、`PanelBorderBrush`、`PrimaryTextBrush`、`SecondaryTextBrush`、`AccentBrush`、`RowHoverBrush`、`RowSelectedBrush` 等语义资源。
- 已切换并截图检查 light、dark、aquatic、desert、dusk、night-sky 六套主题的总览界面。

基于 Release `net11.0-windows` 构建的实测结果：

| 测量项 | 结果 |
| --- | --- |
| 优化前进程采集于线 | 平均 `1458.7 ms`，p95 `1514.7 ms` |
| 优化后进程采集 | 平均 `83.7 ms`，p95 `97.3 ms` |
| 系统指标采集 | 平均 `15.8 ms`，p95 `18.8 ms` |
| 网络连接采集 | 平均 `42.9 ms`，p95 `48.9 ms` |
| 完整应用主动监控 45 秒采样 | 平均 CPU `5.34%`，p95 样本 `17.69%`，平均工作集 `210.4 MB`，最终工作集 `217.1 MB` |
| 完整应用最小化 20 秒采样 | 平均 CPU `0.02%`，工作集 `131.4 MB`，私有内存 `46.8 MB` |

## 第三方开源组件审计（2026-05-20）

检查方式：`dotnet restore NexusDash.slnx`、`dotnet list src/NexusDash/NexusDash.csproj package --include-transitive`、NuGet `.nuspec`、NuGet.org 与源码仓库信息。优先接受 MIT / Apache-2.0 / BSD；LGPL-3.0 等其它开源协议在源码与传递依赖均可追溯时单独标注。

整改：

- 新增 `Directory.Packages.props`，直接依赖统一走中央包管理。
- `AtomUI.Desktop.Controls` 从 `5.1.5-build.11` 升级到 `6.0.0-build.3`，对齐当前 Zhijian 于线。
- `Avalonia` / `Avalonia.Desktop` 从 `11.3.12` 升级到 `12.0.3`。
- 显式添加 `ReactiveUI.Avalonia` `12.0.1`，因为启动代码直接调用了它的 Avalonia 集成 API。
- 移除 `Avalonia.Diagnostics`；该包可用版本仍停留在 11.x，不适合 Avalonia 12 于线。
- `System.Diagnostics.PerformanceCounter` / `System.Management` 保持最新稳定版 `10.0.8`；更高的 11.x 版本仍是 preview。
- `Tmds.DBus.Protocol` pin 到 `0.93.0`，对齐已审计的 Zhijian 于线。

| 包 | 使用范围 | 协议 | 源码/项目地址 | 结论 |
| --- | --- | --- | --- | --- |
| `AtomUI.Desktop.Controls` `6.0.0-build.3` | 桌面控件 | LGPL-3.0 | https://github.com/AtomUI/AtomUI | NuGet 包指向公开源码；按“源码与传递依赖可追溯”通过 |
| `Avalonia` / `Avalonia.Desktop` | 桌面运行时 | MIT | https://github.com/AvaloniaUI/Avalonia | 通过，已升级到 `12.0.3` |
| `ReactiveUI.Avalonia` `12.0.1` | Avalonia ReactiveUI 集成 | MIT | https://github.com/reactiveui/reactiveui | 通过，为应用启动集成显式声明直接引用 |
| `System.Diagnostics.PerformanceCounter` / `System.Management` | Windows 系统指标 | MIT | https://github.com/dotnet/dotnet | 通过，稳定版 `10.0.8` |
| `Tmds.DBus.Protocol` | Avalonia Linux DBus 传递依赖 | MIT | https://github.com/tmds/Tmds.DBus | 通过，pin 到 `0.93.0` |

传递依赖检查结论：AtomUI 链路中的 `AtomUI.Core`、`AtomUI.Controls.Shared`、`AtomUI.Fonts.AlibabaSans`、`AtomUI.Icons.AntDesign`、`AtomUI.Native` 均来自 https://github.com/AtomUI/AtomUI，源码开放；Avalonia / SkiaSharp / ANGLE、ReactiveUI / Splat、Svg.Controls.Avalonia / Svg.*、ExCSS、DynamicData、HarfBuzzSharp、MicroCom.Runtime 均有公开源码。有效 restore 未发现 `AvaloniaUI.DiagnosticsSupport`、`Semi.Avalonia.*` 黑盒扩展或已知高危包告警。
## 包版本维护约定

XML 文件统一使用两个空格缩进。`Directory.Packages.props` 统一承载 NuGet 中央包管理开关和包版本变量，包括 `AvaloniaVersion` 等共享版本属性；`Directory.Build.props` 仅保留项目构建、编译选项和 NuGet 元数据。仓库如引用 `VC-LTL`、`YY-Thunks`，这两个兼容旧版操作系统的特殊包必须使用最新预览版。
