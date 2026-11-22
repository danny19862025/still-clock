#ifndef AppVersion
  #define AppVersion "2.7.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\Still-win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{FA04A93D-5937-4B9B-9776-DC894E39DA2C}
AppName=Still
AppVersion={#AppVersion}
AppPublisher=danny19862025
AppPublisherURL=https://github.com/danny19862025/still-clock
AppSupportURL=https://github.com/danny19862025/still-clock/issues
AppUpdatesURL=https://github.com/danny19862025/still-clock/releases
DefaultDirName={localappdata}\Programs\Still
DefaultGroupName=Still
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir={#OutputDir}
OutputBaseFilename=Still-Setup-win-x64
SetupIconFile=..\src\Still.App\still.ico
UninstallDisplayIcon={app}\Still.exe
UninstallDisplayName=Still
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\Still.Clock.SingleInstance
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}.0

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "screenshots\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Still"; Filename: "{app}\Still.exe"; Parameters: "--settings"; WorkingDir: "{app}"
Name: "{autodesktop}\Still"; Filename: "{app}\Still.exe"; Parameters: "--settings"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Still.exe"; Parameters: "--settings"; Description: "Launch Still"; Flags: nowait postinstall skipifsilent
; In-app updates run Setup silently with /RELAUNCH=1, so reopen the clock afterwards.
Filename: "{app}\Still.exe"; WorkingDir: "{app}"; Flags: nowait; Check: ShouldRelaunch

[Code]
function ShouldRelaunch: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Still', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\Still.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Still');
end;
