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

CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[InstallDelete]
Type: filesandordirs; Name: "{app}\runtimes"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.json"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Start Perch now"; Flags: nowait postinstall skipifsilent

[Code]
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

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Settings: String;
  Cache: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  RegDeleteValue(HKEY_CURRENT_USER,
                 'Software\Microsoft\Windows\CurrentVersion\Run', 'Perch');

  Settings := ExpandConstant('{userappdata}\Perch');
  Cache := ExpandConstant('{localappdata}\Perch');

  if not DirExists(Settings) and not DirExists(Cache) then
    Exit;

  if MsgBox('Also remove your Perch settings and rules?' + #13#10#13#10 +
            'Choose No to keep them.',
            mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
  begin
    DelTree(Settings, True, True, True);
    DelTree(Cache, True, True, True);
  end;
end;
