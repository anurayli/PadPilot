; PadPilot kurulum betigi (Inno Setup 6). Derlemek icin: installer\build.ps1
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define Publish "..\artifacts\publish"
#define Redist "redist"
#define ViGEmSetup "ViGEmBus_1.22.0_x64_x86_arm64.exe"
#define HidHideSetup "HidHide_1.5.230_x64.exe"

[Setup]
AppId={{6E2F0B8C-3C1A-4E0E-9B5B-7B0A2D9C4F11}
AppName=PadPilot
AppVersion={#AppVersion}
AppVerName=PadPilot {#AppVersion}
AppPublisher=PadPilot
AppPublisherURL=https://github.com/anurayli/PadPilot
AppSupportURL=https://github.com/anurayli/PadPilot/issues
AppUpdatesURL=https://github.com/anurayli/PadPilot/releases
DefaultDirName={autopf}\PadPilot
DefaultGroupName=PadPilot
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
WizardStyle=modern
SetupIconFile=..\src\PadPilot.App\padpilot.ico
UninstallDisplayIcon={app}\PadPilot.exe
OutputDir=..\artifacts
OutputBaseFilename=PadPilot-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
AppMutex=PadPilot.TekKopya
CloseApplications=yes
LicenseFile=..\LICENSE
ShowLanguageDialog=auto
UsedUserAreasWarning=no

[Languages]
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
tr.Drivers=Gerekli sürücüler:
en.Drivers=Required drivers:
tr.TaskViGEm=ViGEmBus'u kur (sanal Xbox/DS4 pad — gerekli)
en.TaskViGEm=Install ViGEmBus (virtual Xbox/DS4 pad — required)
tr.TaskHidHide=HidHide'ı kur (oyunlarda çift girişi önler — önerilir)
en.TaskHidHide=Install HidHide (prevents double input in games — recommended)
tr.Startup=Windows açılınca başlat
en.Startup=Start with Windows
tr.InstallingViGEm=ViGEmBus kuruluyor...
en.InstallingViGEm=Installing ViGEmBus...
tr.InstallingHidHide=HidHide kuruluyor...
en.InstallingHidHide=Installing HidHide...
tr.Launch=PadPilot'u başlat
en.Launch=Launch PadPilot

[Tasks]
Name: "vigem"; Description: "{cm:TaskViGEm}"; GroupDescription: "{cm:Drivers}"; Check: not ViGEmInstalled
Name: "hidhide"; Description: "{cm:TaskHidHide}"; GroupDescription: "{cm:Drivers}"; Check: not HidHideInstalled
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "{cm:Startup}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#Publish}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Redist}\{#ViGEmSetup}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Tasks: vigem
Source: "{#Redist}\{#HidHideSetup}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Tasks: hidhide

[Icons]
Name: "{group}\PadPilot"; Filename: "{app}\PadPilot.exe"
Name: "{autodesktop}\PadPilot"; Filename: "{app}\PadPilot.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PadPilot"; \
  ValueData: """{app}\PadPilot.exe"" --tray"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{tmp}\{#ViGEmSetup}"; Parameters: "/exenoui /qn /norestart"; StatusMsg: "{cm:InstallingViGEm}"; Tasks: vigem; Flags: waituntilterminated
Filename: "{tmp}\{#HidHideSetup}"; Parameters: "/exenoui /qn /norestart"; StatusMsg: "{cm:InstallingHidHide}"; Tasks: hidhide; Flags: waituntilterminated
Filename: "{app}\PadPilot.exe"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v PadPilot /f"; Flags: runhidden; RunOnceId: "RemoveStartup"

[Code]
function ViGEmInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\ViGEmBus');
end;

function HidHideInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\HidHide');
end;
