; Perch installer — per-user, so it never asks for administrator rights.
; Build with:  ISCC.exe installer\Perch.iss
; Expects a self-contained publish in ..\publish (see build-installer.ps1).

#define AppName        "Perch"
#define AppPublisher   "Mathias Andresen"
#define AppUrl         "https://github.com/MatternPL/Perch"
#define AppExeName     "Perch.exe"

#ifndef AppVersion
  #define AppVersion   "1.1.0"
#endif

#ifndef PayloadDir
  #define PayloadDir   "..\publish"
#endif

[Setup]
AppId={{E6E7372F-BA4A-4BA8-8948-3C20A071BDBE}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}

; Install into the user's own profile: no UAC prompt, and "start with Windows"
; works without any elevation. PrivilegesRequiredOverridesAllowed lets someone
; who wants a machine-wide install ask for one on the command line.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}

OutputDir=..\dist
OutputBaseFilename=PerchSetup-{#AppVersion}
SetupIconFile=..\src\Perch\Assets\perch.ico
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Perch lives in the tray, so the Restart Manager rarely spots it on its own.
; PrepareToInstall below closes it explicitly instead.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Start Perch now"; Flags: nowait postinstall skipifsilent

[Code]
// Perch may be sitting in the tray while its own files are being replaced.
procedure StopPerch;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExeName}', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopPerch;
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopPerch;
  Result := True;
end;

// Settings and the WebView2 profile live outside the install folder; offer to
// take them too, but never delete them without asking.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Settings: String;
  Cache: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  // Perch writes this entry itself from its own settings, so the installer never
  // creates it - but it must not be left pointing at a deleted executable.
  RegDeleteValue(HKEY_CURRENT_USER,
                 'Software\Microsoft\Windows\CurrentVersion\Run', 'Perch');

  Settings := ExpandConstant('{userappdata}\Perch');
  Cache := ExpandConstant('{localappdata}\Perch');

  if not DirExists(Settings) and not DirExists(Cache) then
    Exit;

  if MsgBox('Remove your Perch settings, rules and browsing data as well?' + #13#10#13#10 +
            'Choose No to keep them for a future install.',
            mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
  begin
    DelTree(Settings, True, True, True);
    DelTree(Cache, True, True, True);
  end;
end;
