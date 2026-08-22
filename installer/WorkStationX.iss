; Inno Setup script for WorkStationX
;
; Build it in two steps:
;   1. dotnet publish WorkStationX/WorkStationX.csproj -c Release -o publish
;   2. open this file in Inno Setup and press Build (or run ISCC.exe on it)
;
; The published folder is self-contained, so the machine being installed to does
; not need the .NET runtime.

#define AppName        "WorkStationX"
#define AppVersion     "1.0.0"
#define AppPublisher   "WorkStationX"
#define AppExe         "WorkStationX.exe"

[Setup]
AppId={{8F3C7A21-5D64-4E9B-9C42-1A7E5B0D3F88}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\dist
OutputBaseFilename=WorkStationX-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Per-user install by default: no admin prompt, and it keeps %APPDATA% data
; belonging to the person who installed it.
PrivilegesRequiredOverridesAllowed=dialog
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; \
    GroupDescription: "Shortcuts:"
Name: "startup"; Description: "Start WorkStationX when Windows starts"; \
    GroupDescription: "Startup:"; Flags: unchecked

[Files]
; The whole published folder, including the .NET runtime it carries.
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
; A Start Menu shortcut is what lets Windows attribute notifications to the app.
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Logs only. The database and settings are deliberately left behind so an
; uninstall/reinstall does not wipe someone's workspaces and history.
Type: filesandordirs; Name: "{userappdata}\WorkStationX\logs"
