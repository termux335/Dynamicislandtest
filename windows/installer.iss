; Dynamic Island - Inno Setup Script
; Sesuai CLAUDE.windows.md (W4: installer MSIX atau Inno Setup).
; Dapat dikompilasi dengan Inno Setup 6 (iscc installer.iss).
; Pemasangan per-pengguna (tanpa hak administrator).

#define MyAppName "Dynamic Island"
#define MyAppVersion "0.6.0-beta"
#define MyAppPublisher "Dynamic Island Project"
#define MyAppURL "https://github.com/termux335/Dynamicislandtest"
#define MyAppExeName "DynamicIsland.App.exe"

[Setup]
AppId={{D151A4D0-151A-4D01-9041-000000000001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\DynamicIsland
DisableDirPage=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputBaseFilename=DynamicIsland-{#MyAppVersion}-Setup
OutputDir=..\dist
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "indonesian"; MessagesFile: "compiler:Languages\Indonesian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Jalankan otomatis saat login Windows"; GroupDescription: "Mulai:"

[Files]
Source: "DynamicIsland.App\bin\Release\net8.0-windows10.0.19041.0\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Bila belum di-publish, fallback ke bin Release:
Source: "DynamicIsland.App\bin\Release\net8.0-windows10.0.19041.0\*"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DynamicIsland"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
