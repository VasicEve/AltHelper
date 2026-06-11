; MouseToPad installer (Inno Setup 6).
;
; Build steps:
;   1. dotnet publish MouseToPad.csproj -c Release -r win-x64 --no-self-contained
;   2. ISCC.exe installer.iss
; Output: installer\MouseToPadSetup.exe
;
; Installs per-user (no UAC prompt) to %LOCALAPPDATA%\Programs\MouseToPad with:
;   - Start Menu shortcut
;   - Desktop shortcut                  (task, on by default)
;   - Startup entry: shortcut in shell:startup so it launches at sign-in (task, on by default)
; Requires the .NET 8 Desktop Runtime on the target machine.

#define AppName "MouseToPad"
#define AppVersion "1.7.0"
#define AppExe "MouseToPad.exe"
#define PublishDir "bin\Release\net8.0-windows\win-x64\publish"

[Setup]
; never change AppId between versions — it is how upgrades find the old install
AppId={{8C2D7F4A-5E1B-4F3A-A9C6-D0B4E7251F9E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Lee Coleman
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=installer
OutputBaseFilename=MouseToPadSetup
SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; matches the Mutex name in App.xaml.cs so setup asks to close a running instance
AppMutex=MouseToPad_SingleInstance

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "startup"; Description: "Start {#AppName} automatically when you sign in"; GroupDescription: "Startup:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent
