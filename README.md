# NexusDash

[简体中文](README.zh-CN.md) | English

NexusDash is an Avalonia desktop dashboard for local system monitoring. The project uses `NexusDash.slnx` and central NuGet package management through `Directory.Packages.props`.

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
