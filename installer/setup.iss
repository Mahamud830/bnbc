[Setup]
AppName=BNBC Ultimate Pro
AppVersion=0.1.0
AppPublisher=BNBC Ultimate Pro
DefaultDirName={autopf}\BNBC Ultimate Pro
DefaultGroupName=BNBC Ultimate Pro
OutputBaseFilename=BNBC_Ultimate_Pro_Setup
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
UninstallDisplayIcon={app}\BnbcUltimatePro.exe

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\BNBC Ultimate Pro"; Filename: "{app}\BnbcUltimatePro.exe"
Name: "{autodesktop}\BNBC Ultimate Pro"; Filename: "{app}\BnbcUltimatePro.exe"

[Run]
Filename: "{app}\BnbcUltimatePro.exe"; Description: "Launch BNBC Ultimate Pro"; Flags: nowait postinstall skipifsilent
