; ===================================================================
;  DXPeditions Tracker - installer
;
;  Build with Inno Setup 6 or newer (run from the repo root):
;      iscc "Installer\InnoSetup\DXPeditions Tracker.iss"
;
;  Expects the published self-contained single-file exe at:
;      publish\DXPeditions.App.exe
;  (build.ps1, at the repo root, puts it there)
; ===================================================================

#define MyAppName "DXPeditions Tracker"
#define MyAppPublisher "K5JSG"
#define MyAppURL "https://github.com/K5JSG/DXPeditions"
#define MyAppExeName "DXPeditions.App.exe"

; Overridable from the command line: iscc /DMyAppVersion=1.1.0 ...
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
; Keep this GUID stable forever: it is how Windows recognises an upgrade of
; the same product rather than a second installation.
AppId={{2D4E9B7C-8F1A-4C6E-9A3D-5B7F2E8C1D6A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}

DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes
LicenseFile=..\..\License.txt

; Writing to Program Files needs admin - the app itself does not require
; elevation to run (all its own data lives under %LocalAppData%), just to
; install.
PrivilegesRequired=admin

OutputDir=..\..\dist
OutputBaseFilename={#MyAppName} Setup {#MyAppVersion}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Windows 10 1809 or newer
MinVersion=10.0.17763

; Offer to shut the app down instead of demanding a reboot
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; \
    GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme skipifsourcedoesntexist
Source: "..\..\License.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; \
    Flags: postinstall nowait skipifsilent

[Code]

// Stop a running instance before installing or uninstalling, otherwise the
// exe is locked and the file copy fails.
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'),
       '/C taskkill /F /IM "{#MyAppExeName}" >nul 2>&1',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningApp();
  Result := True;
end;
