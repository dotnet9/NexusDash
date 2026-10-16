# 打包 Linux .deb 安装包（dpkg-deb，在 ubuntu runner 上运行）。
# 用法：pwsh scripts/package_linux_deb.ps1 -RuntimeIdentifier linux-x64 -Version 1.2.3
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("linux-x64", "linux-arm64")]
    [string] $RuntimeIdentifier,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $SourceDirectory = "",
    [string] $OutputDirectory = "",
    [switch] $Force
)

$ErrorActionPreference = "Stop"
$scriptRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    $SourceDirectory = Join-Path $repositoryRoot "artifacts/publish/$RuntimeIdentifier/NexusDash"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts/release"
}

if (-not (Test-Path -LiteralPath $SourceDirectory)) {
    throw "Publish output was not produced: $SourceDirectory"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$architecture = if ($RuntimeIdentifier -eq "linux-arm64") { "arm64" } else { "amd64" }
$packageName = "NexusDash-v$Version-$RuntimeIdentifier.deb"
$outputPath = Join-Path $OutputDirectory $packageName
if ((Test-Path -LiteralPath $outputPath) -and -not $Force) {
    throw "Output already exists (use -Force to overwrite): $outputPath"
}

# 每次打包使用唯一临时目录，只清理当前任务创建的暂存文件。
$staging = Join-Path ([IO.Path]::GetTempPath()) ('NexusDash-deb-' + [Guid]::NewGuid().ToString('N'))
try {
New-Item -ItemType Directory -Path (Join-Path $staging "DEBIAN") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $staging "usr/local/share/NexusDash") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $staging 'usr/local/bin'), (Join-Path $staging 'usr/share/applications'), (Join-Path $staging 'usr/share/pixmaps') -Force | Out-Null

# 程序文件
$sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path
$appPath = Join-Path $staging 'usr/local/share/NexusDash'
if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'NexusDash') -PathType Leaf)) { throw '发布目录缺少主程序。' }
Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($sourceRoot, $_.FullName)
    if ($_.Extension -in '.pdb', '.dbg' -or $relative -match '(^|[\\/])[^\\/]+\.dSYM([\\/]|$)') { return }
    $destination = Join-Path $appPath $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'logo.png') -Destination (Join-Path $staging 'usr/share/pixmaps/nexusdash.png')
# Shell 入口使用 LF，原样保留带空格的参数。
[IO.File]::WriteAllText((Join-Path $staging 'usr/local/bin/nexusdash'), "#!/bin/sh`n" + 'exec /usr/local/share/NexusDash/NexusDash "$@"' + "`n")
$desktop = @('[Desktop Entry]', 'Type=Application', 'Name=NexusDash', 'Exec=nexusdash', 'Icon=nexusdash', 'Terminal=false', 'Categories=System;Monitor;') -join "`n"
[IO.File]::WriteAllText((Join-Path $staging 'usr/share/applications/nexusdash.desktop'), $desktop + "`n")

# control（dpkg-deb 要求 DEBIAN/control 为 root 所属、644）
$installedSizeKb = [Math]::Ceiling(
    ((Get-ChildItem -LiteralPath (Join-Path $staging 'usr') -Recurse -File | Measure-Object Length -Sum).Sum) / 1KB)
$control = @"
Package: nexusdash
Version: $Version
Architecture: $architecture
Maintainer: Dotnet9 <dotnet9.com@gmail.com>
Installed-Size: $installedSizeKb
Depends: libc6, libx11-6, libxrandr2, libxrender1, libxi6, libfontconfig1, libfreetype6, libglib2.0-0
Section: utils
Priority: optional
Description: NexusDash 桌面系统监控仪表盘
"@
Set-Content -LiteralPath (Join-Path $staging "DEBIAN/control") -Value ($control + [Environment]::NewLine) -Encoding UTF8

# dpkg-deb 需要 posix 权限语义；control 文件权限固定 644
if ($IsLinux) {
    & chmod 755 (Join-Path $appPath 'NexusDash') (Join-Path $staging 'usr/local/bin/nexusdash')
    if ($LASTEXITCODE -ne 0) { throw '无法设置程序启动权限。' }
    & chmod 644 (Join-Path $staging "DEBIAN/control")
    if ($LASTEXITCODE -ne 0) { throw '无法设置 DEB 元数据权限。' }
}

# 补齐独立安装包的图标、品牌和软件管理器元数据。

& (Join-Path $scriptRoot 'installer/linux/Write-LinuxPackageMetadata.ps1') -StagingDirectory $staging -RepositoryRoot $repositoryRoot -Version $Version

dpkg-deb --build --root-owner-group $staging $outputPath
if ($LASTEXITCODE -ne 0) {
    throw "dpkg-deb failed for $RuntimeIdentifier"
}
if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
    throw "Deb was not produced: $outputPath"
}

$sha = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$sha  $packageName" | Set-Content -LiteralPath "$outputPath.sha256" -Encoding ASCII


Write-Host "Deb 打包完成：$outputPath"

} finally {
    $resolved = [IO.Path]::GetFullPath($staging)
    $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($temporary, [StringComparison]::OrdinalIgnoreCase)) { throw '拒绝清理临时目录之外的路径。' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
