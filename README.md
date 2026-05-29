# NexusDash

[简体中文](README.zh-CN.md) | English

NexusDash is an Avalonia desktop dashboard for local system monitoring. The project uses `NexusDash.slnx` and central NuGet package management through `Directory.Packages.props`.

## Performance and UI Refresh Optimization (2026-05-29)

Recent monitoring and UI work focused on reducing NexusDash's own resource usage while keeping the dashboard responsive.

Main changes:

- Replaced the live metric chart path with a lightweight Avalonia-rendered control, avoiding per-refresh plot bitmap generation and removing the `ScottPlot.Avalonia` dependency from the app.
- Split refresh cadence by data cost: lightweight system metrics refresh every 2 seconds, while process snapshots, process tree updates, treemap data, and network connection snapshots refresh at a lower cadence of about 6 seconds.
- Optimized process telemetry on Windows by using Toolhelp/native process snapshots and native IO counters, while moving expensive static metadata refreshes to a 2-minute interval.
- Removed per-refresh disk enumeration and active TCP connection counting from the system monitor because those values are not used by the current overview UI.
- Added icon decode caching and limited icon extraction to application processes.
- Replaced the third-party log viewer surface with a lightweight in-app operation log so theme colors and refresh cost are controlled by NexusDash.
- Automatically pauses monitoring refresh while the main window is minimized, then resumes when the window is restored.
- Consolidated theme-sensitive surfaces through semantic resources such as `AppBackgroundBrush`, `PanelBackgroundBrush`, `PanelAltBackgroundBrush`, `PanelBorderBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`, `AccentBrush`, `RowHoverBrush`, and `RowSelectedBrush`.
- Verified the overview screen across light, dark, aquatic, desert, dusk, and night-sky themes.

Measured results from the Release `net11.0-windows` build on the test machine:

| Measurement | Result |
| --- | --- |
| Process telemetry baseline before optimization | avg `1458.7 ms`, p95 `1514.7 ms` |
| Process telemetry after optimization | avg `83.7 ms`, p95 `97.3 ms` |
| System metrics collection | avg `15.8 ms`, p95 `18.8 ms` |
| Network connection collection | avg `42.9 ms`, p95 `48.9 ms` |
| Full app active monitoring, 45-second sample | avg CPU `5.34%`, p95 sample `17.69%`, working set avg `210.4 MB`, final working set `217.1 MB` |
| Full app minimized, 20-second sample | avg CPU `0.02%`, working set `131.4 MB`, private memory `46.8 MB` |

## Third-Party Open Source Audit (2026-05-20)

Checked with `dotnet restore NexusDash.slnx`, `dotnet list src/NexusDash/NexusDash.csproj package --include-transitive`, NuGet `.nuspec` metadata, NuGet.org, and upstream source repositories. MIT / Apache-2.0 / BSD are preferred; LGPL-3.0 and other source-open licenses are explicitly marked when source and transitive dependencies are traceable.

Remediation:

- Added `Directory.Packages.props` so direct dependencies use central package management.
- Updated `AtomUI.Desktop.Controls` from `5.1.5-build.11` to `6.0.0-build.3`, matching the current Zhijian baseline.
- Updated `Avalonia` / `Avalonia.Desktop` from `11.3.12` to `12.0.3`.
- Added an explicit `ReactiveUI.Avalonia` `12.0.1` reference because the startup code calls its Avalonia integration API directly.
- Removed `Avalonia.Diagnostics`; the available package line is still 11.x and is not suitable for the Avalonia 12 baseline.
- Kept `System.Diagnostics.PerformanceCounter` / `System.Management` on the latest stable `10.0.8`; the newer 11.x packages are preview builds.
- Pinned `Tmds.DBus.Protocol` to `0.93.0`, matching the audited Zhijian baseline.

| Package | Usage | License | Source | Status |
| --- | --- | --- | --- | --- |
| `AtomUI.Desktop.Controls` `6.0.0-build.3` | Desktop controls | LGPL-3.0 | https://github.com/AtomUI/AtomUI | NuGet package points to public source; approved under the source-traceable non-preferred license rule |
| `Avalonia` / `Avalonia.Desktop` | Desktop runtime | MIT | https://github.com/AvaloniaUI/Avalonia | Approved, updated to `12.0.3` |
| `ReactiveUI.Avalonia` `12.0.1` | Avalonia ReactiveUI integration | MIT | https://github.com/reactiveui/reactiveui | Approved, explicit direct reference for app startup integration |
| `System.Diagnostics.PerformanceCounter` / `System.Management` | Windows system metrics | MIT | https://github.com/dotnet/dotnet | Approved, stable `10.0.8` |
| `Tmds.DBus.Protocol` | Avalonia Linux DBus transitive dependency | MIT | https://github.com/tmds/Tmds.DBus | Approved, pinned to `0.93.0` |

Transitive dependency check: AtomUI transitive packages including `AtomUI.Core`, `AtomUI.Controls.Shared`, `AtomUI.Fonts.AlibabaSans`, `AtomUI.Icons.AntDesign`, and `AtomUI.Native` are from https://github.com/AtomUI/AtomUI and are source-open. Avalonia, SkiaSharp, ANGLE, ReactiveUI, Splat, Svg.Controls.Avalonia, Svg.*, ExCSS, DynamicData, HarfBuzzSharp, and MicroCom.Runtime are source-open. Active restore assets do not contain `AvaloniaUI.DiagnosticsSupport`, `Semi.Avalonia.*` black-box extensions, or known high-risk package warnings.
