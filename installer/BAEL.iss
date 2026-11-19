#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{EF585152-8423-4800-8F95-F1B5A12E4619}
AppName=BAEL-UMAN
AppVersion={#AppVersion}
AppPublisher=YisusCraiz
AppPublisherURL=https://github.com/YisusCraiz/Bael-Chat
DefaultDirName={localappdata}\Programs\BAEL
DefaultGroupName=BAEL-UMAN
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=BAEL-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Bael\Assets\bael-unicorn.ico
UninstallDisplayIcon={app}\BAEL-UMAN.exe
CloseApplications=yes
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; Flags: unchecked

[InstallDelete]
Type: files; Name: "{app}\Bael.exe"
Type: files; Name: "{app}\Bael.dll"
Type: files; Name: "{app}\Bael.deps.json"
Type: files; Name: "{app}\Bael.runtimeconfig.json"
Type: files; Name: "{group}\BAEL.lnk"
Type: files; Name: "{autodesktop}\BAEL.lnk"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BAEL-UMAN"; Filename: "{app}\BAEL-UMAN.exe"
Name: "{autodesktop}\BAEL-UMAN"; Filename: "{app}\BAEL-UMAN.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BAEL-UMAN.exe"; Description: "Abrir BAEL-UMAN"; Flags: nowait postinstall skipifsilent
