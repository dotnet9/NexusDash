# 更新日志

## 0.1.9 (2026-10-04)

### 全平台 NativeAOT + 安装包

- 发布矩阵补齐为 5 平台：win-x64、linux-x64、linux-arm64、osx-x64（Intel）、osx-arm64（Apple Silicon），全部 NativeAOT（完整反射元数据保全，单线程 ILC）；目标框架统一下调到 .NET 10（10 正式版工具链在 macOS 上的 AOT 链接稳定，替代 .NET 11 preview 的 swift auto-link 缺陷）。
- 非 Windows 平台改用真正的安装包：Linux 产出 `.deb`（amd64/arm64），macOS 产出 `.dmg`（Intel/Apple Silicon），Windows 维持既有安装器/分发形态。
- 平台相关功能尚未适配时，应用内给出友好提示（如「当前平台功能正在开发中」），不阻塞启动与其余功能。

## 0.1.8 (2026-10-04)

### 全平台 NativeAOT 发布

- 所有发布平台（Windows / Linux / macOS）统一走 NativeAOT：完整反射元数据保全（`IlcGenerateCompleteTypeMetadata` + `IlcTrimMetadata=false`，Prism/DryIoc 反射兼容），单线程 ILC；启动速度与内存占用进一步优化，发布件为原生单可执行文件。
- CI 矩阵按目标系统分发 runner（Linux 任务安装 clang/zlib1g-dev，linux-arm64 使用 arm64 runner），win-x86 不受 NativeAOT 支持保持自包含单文件（如适用）。
- `scripts/publish.ps1` 与 CI 使用同一套发布参数。

## 0.1.7 (2026-10-04)

### 数据目录规范化

- 用户数据统一迁移到系统标准应用数据目录：Windows `%LOCALAPPDATA%`、Linux `~/.local/share`、macOS `~/Library/Application Support`（`Environment.SpecialFolder.LocalApplicationData`）；旧版 Roaming（`%APPDATA%`）位置的数据在首次启动时自动迁移，旧目录原样保留作备份。
- 统一发布脚本入口 `scripts/publish.ps1`（`-RuntimeIdentifier`/`-Version`，输出 `artifacts/publish/<rid>/`），本地发布与 CI 同一套逻辑。
## 0.1.6 (2026-10-04)

### 更新检查

- 「关于」设置页新增检查更新：版本号经 GitHub 网页端点（`releases/latest` 302 落点 + `expanded_assets/{tag}`）获取，不碰 api.github.com 的每小时配额；发现新版本就地提示并打开发布页。四语言文案同步。

## 0.1.2 (2026-06-08)

- 🔨[优化]-补齐根目录 logo.svg、logo.png、logo.ico 三件套，子工程通过 MSBuild Link 引用根 logo，避免维护多份图标副本。
- 🔨[优化]-统一目标框架：NuGet 包项目支持 `net8.0;net10.0`，Demo、App、测试与内部应用项目升级到 `net11.0` / `net11.0-windows`。
- 🔨[优化]-保留运行时帮助、Markdown 示例、内置备忘录和业务设计文档，仅收敛仓库级重复文档入口。

## 0.1.1 (2026-06-08)

- 统一版本号维护入口，只在仓库根目录 `Directory.Build.props` 中定义 `<Version>`。
- 清理英文/双语文档入口，后续仅维护简体中文文档。
- 完善 NuGet 发布配置，补充 Source Link、符号包和标签格式规范。


## 2026-05-29

- 将设置中心改为主界面内嵌工具页，移除独立设置窗口与 Prism SettingsRegion 注册链路，修复重复打开设置时的崩溃。
- 优化设置页布局，调整外观、更新日志、关于三个页面的客户区排版，并移除状态栏重复设置入口。
- 新增跟随系统、浅色、深色、水色、沙漠、暮色、夜空 7 个主题选项，并持久化主题选择。
- 使用 Ursa/Semi 图标资源替换多处文本和手绘图标，统一侧边栏、状态栏、进程列表和确认对话框图标风格。
- 优化语言切换和工具页切换体验，延后进程树本地化刷新与不可见页面的进程树重建，降低切换卡顿。
- 优化实时刷新性能，将进程快照整理移出 UI 线程，并改为增量更新进程行、进程树和状态栏导出快照，降低点击与 Tab 切换卡顿。
- 更新应用 Logo 为更简洁的项目标识，并同步 PNG、SVG、ICO 资源。

## 2026-05-21

- 新增进程树实时监控，支持应用、后台进程与 Windows 进程分组。
- 新增进程图标显示，应用进程优先展示真实可执行文件图标。
- 新增按进程名、发布者、PID 搜索进程，搜索结果按分组平铺展示。
- 新增进程 TCP/UDP 通信面板，可查看选中进程的连接数量和端点信息。
- 新增右键结束进程与结束进程树的确认流程。
- 新增深色/浅色主题切换和多语言切换。
- 新增设置中心，集中管理外观、更新日志和关于信息。
- 新增进程列表列显隐菜单，支持持久化非关键列显示状态。
## 2026-06-08 仓库规范整理

- 统一文档维护入口：每个仓库只保留根目录 `README.md` 和根目录 `UpdateLog.md`，清理重复日志、英文文档和语言切换入口。
- 统一版本维护入口：包版本只在仓库根目录 `Directory.Build.props` 的 `<Version>` 节点维护，移除散落的程序集版本配置。
- 不再维护 `global.json`，SDK 选择交给本机或 CI 环境；NuGet 包和应用的目标框架在项目文件中明确声明。
- 统一 NuGet 包文档入口：包 README 统一引用仓库根 `README.md`，更新日志统一引用仓库根 `UpdateLog.md`。
