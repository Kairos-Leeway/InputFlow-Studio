#define MyAppVersion "2.2.1"
[Setup]
AppId={{39D59D48-BCB8-43F4-8824-0059BE653B56}
AppName=InputFlow Studio
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\InputFlow Studio
DefaultGroupName=InputFlow Studio
UsePreviousAppDir=yes
OutputDir=installer
OutputBaseFilename=Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
SetupIconFile=Assets\App.ico
UninstallDisplayIcon={app}\InputFlowStudio.exe
LicenseFile=EULA.rtf
[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
[Files]
Source: "EULA.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "EULA.rtf"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish-v2\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[InstallDelete]
Type: files; Name: "{app}\MouseFlowStudio.exe"
Type: files; Name: "{app}\MouseFlowStudio.dll"
Type: files; Name: "{app}\MouseFlowStudio.deps.json"
Type: files; Name: "{app}\MouseFlowStudio.runtimeconfig.json"
Type: files; Name: "{app}\MouseFlowStudio.pdb"
Type: files; Name: "{autodesktop}\MouseFlow Studio.lnk"
Type: files; Name: "{autoprograms}\MouseFlow Studio\MouseFlow Studio.lnk"
Type: dirifempty; Name: "{autoprograms}\MouseFlow Studio"
[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcut"; Flags: unchecked
[Icons]
Name: "{group}\InputFlow Studio"; Filename: "{app}\InputFlowStudio.exe"
Name: "{autodesktop}\InputFlow Studio"; Filename: "{app}\InputFlowStudio.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\InputFlowStudio.exe"; Description: "Launch InputFlow Studio"; Flags: nowait postinstall skipifsilent
[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if WizardSilent and (ExpandConstant('{param:ACCEPTLICENSE|0}') <> '1') then
  begin
    Log('Silent setup requires reading EULA.txt and explicit /ACCEPTLICENSE=1.');
    Result := False;
  end;
end;
