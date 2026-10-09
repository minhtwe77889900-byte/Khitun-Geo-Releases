#define MyAppName "Khitun Geo"
#define MyAppVersion "1.7.9"
#define MyAppPublisher "Khitun Ivan"
#define MyAppExeName "KhitunGeo.exe"
#define MyAppId "{D6F08F2C-06AF-4F6D-91A0-BA6BD1BC912D}"

[Setup]
AppId={{D6F08F2C-06AF-4F6D-91A0-BA6BD1BC912D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Установщик Khitun Geo
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoVersion=1.7.9.0
DefaultDirName={localappdata}\Programs\Khitun Geo
DefaultGroupName=Khitun Geo
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
UsePreviousPrivileges=yes
DisableDirPage=auto
UsePreviousTasks=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=KhitunGeo_Setup_1_7_9_x64
SetupIconFile=..\src\KhitunGeo\brand\khitun_geo.ico
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
CreateUninstallRegKey=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes
AllowNoIcons=yes
MinVersion=10.0.19041

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные ярлыки:"; Flags: unchecked

[Files]
Source: "..\src\KhitunGeo\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Khitun Geo"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Khitun Geo"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Flags: nowait runasoriginaluser; Check: RestartAfterUpdate


; Inno Setup удаляет только зарегистрированные при установке файлы.
; Чужие файлы в {app} и пользовательские данные сохраняются.

[Code]
function RestartAfterUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:RESTARTAPP|0}') = '1';
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;
