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
; The main window normally hides to the tray when closed. During upgrades,
; Restart Manager must be allowed to force-close the app after its normal close
; request fails, otherwise the tray process can keep published files locked.
CloseApplications=force
; Only track the companion executable. The default filter also tracks every DLL,
; which can cause unrelated .NET apps to block an otherwise safe per-user upgrade.
CloseApplicationsFilter=DesktopCompanion.App.exe
RestartApplications=no
Uninstallable=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Avoid rewriting unchanged .NET runtime DLLs (for example clrjit.dll) during
; application-only upgrades. Still replace same-version app binaries when their
; contents differ, which is common when the assembly version is unchanged.
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: replacesameversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Desktop Companion"; Filename: "{app}\DesktopCompanion.App.exe"
Name: "{autodesktop}\Desktop Companion"; Filename: "{app}\DesktopCompanion.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DesktopCompanion.App.exe"; Description: "Launch Desktop Companion"; Flags: postinstall nowait skipifsilent
