; Inno Setup script for Printer Switch.
; Built by build.ps1:  ISCC.exe /DAppVersion=1.0.0 installer\PrinterSwitch.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef RepoUrl
  #define RepoUrl "https://github.com/"
#endif
#define AppExe "PrinterSwitch.exe"

[Setup]
AppId={{4C786CF4-11D5-4E15-95B4-AF63B27E443A}
AppName=Printer Switch
AppVersion={#AppVersion}
AppVerName=Printer Switch {#AppVersion}
AppPublisher=Boris Pavelka
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}
AppUpdatesURL={#RepoUrl}
VersionInfoVersion={#AppVersion}

; Per-user install by default: no administrator rights, no UAC prompt.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\Printer Switch
DisableProgramGroupPage=yes
DisableReadyPage=no

; Ask the running app to close (it owns this mutex).
AppMutex=PrinterSwitch.Instance
CloseApplications=yes
RestartApplications=no

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041

WizardStyle=modern
WizardSizePercent=110
SetupIconFile=..\src\PrinterSwitch\Assets\app.ico
WizardSmallImageFile=wizard-small.bmp,wizard-small-200.bmp
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName=Printer Switch
LicenseFile=..\LICENSE
ShowLanguageDialog=auto

OutputDir=..\dist
OutputBaseFilename=PrinterSwitch-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4

[Languages]
Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
cs.AppTitle=Přepínač tiskárny
en.AppTitle=Printer Switch
cs.Autostart=Spouštět po přihlášení do Windows
en.Autostart=Start when I sign in to Windows
cs.Launch=Spustit Přepínač tiskárny a nastavit připojení
en.Launch=Start Printer Switch and set up the connection
cs.Setup=Nastavení Přepínače tiskárny
en.Setup=Printer Switch setup
cs.RemoveData=Smazat i nastavení, záznam a SSH klíč aplikace?%n%nPokud aplikaci jen přeinstalováváš, zvol Ne.
en.RemoveData=Also delete the settings, the log and the app's SSH key?%n%nIf you are only reinstalling, choose No.

[Tasks]
Name: "autostart"; Description: "{cm:Autostart}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Files of an older version that are no longer shipped.
Type: filesandordirs; Name: "{app}\*.pdb"

[Icons]
Name: "{autoprograms}\{cm:AppTitle}"; Filename: "{app}\{#AppExe}"
Name: "{autoprograms}\{cm:Setup}"; Filename: "{app}\{#AppExe}"; Parameters: "--setup"
Name: "{autodesktop}\{cm:AppTitle}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PrinterSwitch"; ValueData: """{app}\{#AppExe}"" --background"; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    { The app can turn autostart on by itself, so always clean it up. }
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PrinterSwitch');
    if not UninstallSilent then
      if MsgBox(CustomMessage('RemoveData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(ExpandConstant('{userappdata}\PrinterSwitch'), True, True, True);
        DelTree(ExpandConstant('{localappdata}\PrinterSwitch'), True, True, True);
      end;
  end;
end;
