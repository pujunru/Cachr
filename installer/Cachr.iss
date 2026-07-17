#define AppName "Cachr"
#define AppPublisher "Cachr contributors"
#define AppVersion GetEnv("CACHR_VERSION")
#define PublishDir GetEnv("CACHR_PUBLISH_DIR")

[Setup]
AppId={{8B045A35-08E9-48DB-9FD6-C6552840B71B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Cachr
DefaultGroupName=Cachr
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=Cachr-Setup-win-x64
SetupIconFile=..\src\Cachr\Assets\Cachr.ico
UninstallDisplayIcon={app}\Cachr.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "startup"; Description: "Start Cachr automatically when I sign in"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Cachr"; Filename: "{app}\Cachr.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Cachr"; ValueData: """{app}\Cachr.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\Cachr.exe"; Description: "Launch Cachr"; Flags: nowait postinstall skipifsilent
