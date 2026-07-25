; HorosSaver Windows installer — HorosCode
; Build: iscc installer\HorosSaver.iss  (from repo root, after publish-win-x64.ps1)

#define MyAppName "HorosSaver"
#define MyAppPublisher "HorosCode"
#define MyAppURL "https://horoscode.de"
#define MyAppExeName "HorosSaver.exe"
#define MyAppVersion "1.0.0"
#define PublishDir "..\artifacts\publish\win-x64"
#define SetupIcon "..\src\HorosSaver\Assets\horossaver-icon.ico"

[Setup]
AppId={{A7C3E9F1-4B2D-4E8A-9C1F-010203040506}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\HorosSaver
DefaultGroupName=HorosCode\{#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=HorosSaver-Setup-{#MyAppVersion}-win-x64
SetupIconFile={#SetupIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=force
RestartApplications=no
; User data (profiles/snapshots) lives in %LocalAppData%\HorosCode\HorosSaver — never touched here.

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "user"; Description: "Nur für mich (empfohlen)"
Name: "admin"; Description: "Für alle Benutzer (Program Files)"

[Components]
Name: "main"; Description: "HorosSaver Anwendung"; Types: user admin; Flags: fixed

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon,{#MyAppName}}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
; App binaries only — no user profiles or snapshots under {app}
Name: "{app}"; Permissions: users-modify

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: main

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "HorosCode HorosSaver"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectDir then
  begin
    if WizardIsComponentSelected('admin') then
      WizardForm.DirEdit.Text := ExpandConstant('{autopf}\HorosCode\HorosSaver')
    else
      WizardForm.DirEdit.Text := ExpandConstant('{localappdata}\Programs\HorosSaver');
  end;
end;

[InstallDelete]
; Only remove files shipped by this installer — never touch %LocalAppData%\HorosCode\HorosSaver data.

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
