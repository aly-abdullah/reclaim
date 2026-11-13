; Reclaim installer - Inno Setup 6 (https://jrsoftware.org/isinfo.php)
; Built by build.ps1, which passes the version from Reclaim.csproj.
;
; Installs for all users into Program Files. Reclaim always runs as
; administrator (and at sign-in through an elevated scheduled task), so its
; exe must live in a folder that only administrators can change.

#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\App"
#endif

[Setup]
AppId={{243E7529-3F3B-4C63-85E7-17D7E937C1A3}
AppName=Reclaim
AppVersion={#AppVersion}
AppVerName=Reclaim {#AppVersion}
AppPublisher=Aly Abdullah
AppPublisherURL=https://github.com/aly-abdullah/reclaim
AppSupportURL=https://github.com/aly-abdullah/reclaim/issues
DefaultDirName={autopf}\Reclaim
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=Reclaim-Setup-{#AppVersion}
SetupIconFile=..\Assets\Reclaim.ico
UninstallDisplayIcon={app}\Reclaim.exe
UninstallDisplayName=Reclaim
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dark
WizardSmallImageFile=logo-small.png
AppMutex=Reclaim.Instance
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\Reclaim.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Reclaim"; Filename: "{app}\Reclaim.exe"
Name: "{autodesktop}\Reclaim"; Filename: "{app}\Reclaim.exe"; Tasks: desktopicon

[Run]
; shellexec so Windows shows its usual administrator prompt for Reclaim.
Filename: "{app}\Reclaim.exe"; Description: "{cm:LaunchProgram,Reclaim}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im Reclaim.exe"; Flags: runhidden; RunOnceId: "StopReclaim"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""Reclaim Memory Optimizer"" /F"; Flags: runhidden; RunOnceId: "RemoveStartupTask"
