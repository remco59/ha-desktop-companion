#ifndef AppVersion
  #define AppVersion "0.1.0-preview"
#endif

[Setup]
AppId={{B76B8C8E-2C6D-4C16-9B42-8D83D7B52C11}
AppName=Desktop Companion
AppVersion={#AppVersion}
AppPublisher=Desktop Companion Contributors
DefaultDirName={localappdata}\Programs\Desktop Companion
DefaultGroupName=Desktop Companion
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\artifacts
OutputBaseFilename=Desktop-Companion-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Desktop Companion
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Desktop Companion"; Filename: "{app}\DesktopCompanion.App.exe"
Name: "{autodesktop}\Desktop Companion"; Filename: "{app}\DesktopCompanion.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DesktopCompanion.App.exe"; Description: "Launch Desktop Companion"; Flags: postinstall nowait skipifsilent
