; NexusDash Windows installer.
; Build from the repository root with Inno Setup 6 and pass /DAppVersion=x.y.z.

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\win-x64\NexusDash"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts\release"
#endif

[Setup]
AppId={{{C9D8E7F6-A5B4-4C3D-8E2F-1A0B9C8D7E6F}}
AppName=NexusDash
AppVersion={#AppVersion}
AppPublisher=Dotnet9
AppPublisherURL=https://github.com/dotnet9/NexusDash
AppSupportURL=https://github.com/dotnet9/NexusDash/issues
DefaultDirName={autopf}\NexusDash
DefaultGroupName=NexusDash
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=NexusDash-v{#AppVersion}-win-x64-setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\NexusDash"; Filename: "{app}\NexusDash.exe"
Name: "{autodesktop}\NexusDash"; Filename: "{app}\NexusDash.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Run]
Filename: "{app}\NexusDash.exe"; Description: "{cm:LaunchProgram,NexusDash}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
