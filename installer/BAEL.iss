#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{EF585152-8423-4800-8F95-F1B5A12E4619}
AppName=BAEL
AppVersion={#AppVersion}
AppPublisher=YisusCraiz
AppPublisherURL=https://github.com/YisusCraiz/Bael-Chat
DefaultDirName={localappdata}\Programs\BAEL
DefaultGroupName=BAEL
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
UninstallDisplayIcon={app}\Bael.exe
CloseApplications=yes
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BAEL"; Filename: "{app}\Bael.exe"
Name: "{autodesktop}\BAEL"; Filename: "{app}\Bael.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Bael.exe"; Description: "Abrir BAEL"; Flags: nowait postinstall skipifsilent
