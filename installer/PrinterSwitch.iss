; Inno Setup script for Printer Switch.
; Built by build.ps1:  ISCC.exe /DAppVersion=1.0.0 installer\PrinterSwitch.iss

#ifndef AppVersion
  #define AppVersion "1.1.4"
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
AppPublisher=typekborispav
AppCopyright=Copyright (C) 2026 typekborispav, GPL-3.0-or-later
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}
AppUpdatesURL={#RepoUrl}
VersionInfoVersion={#AppVersion}

; Installs into Program Files for all users (one UAC prompt). Autostart is not
; written here: in an elevated installer HKCU may belong to a different (admin)
; account, so the app's own setup wizard turns it on for the signed-in user.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\Printer Switch
UsePreviousAppDir=no
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
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "sk"; MessagesFile: "compiler:Languages\Slovak.isl"
Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"

[CustomMessages]
en.AppTitle=Printer Switch
cs.AppTitle=Přepínač tiskárny
de.AppTitle=Druckerumschalter
sk.AppTitle=Prepínač tlačiarne
pl.AppTitle=Przełącznik drukarki
en.Launch=Start Printer Switch and set up the connection
cs.Launch=Spustit Přepínač tiskárny a nastavit připojení
de.Launch=Druckerumschalter starten und Verbindung einrichten
sk.Launch=Spustiť Prepínač tlačiarne a nastaviť pripojenie
pl.Launch=Uruchom Przełącznik drukarki i skonfiguruj połączenie
en.Setup=Printer Switch setup
cs.Setup=Nastavení Přepínače tiskárny
de.Setup=Druckerumschalter einrichten
sk.Setup=Nastavenie Prepínača tlačiarne
pl.Setup=Konfiguracja Przełącznika drukarki
en.Notices=Licences and third-party components
cs.Notices=Licence a součásti třetích stran
de.Notices=Lizenzen und Komponenten Dritter
sk.Notices=Licencie a súčasti tretích strán
pl.Notices=Licencje i komponenty firm trzecich
en.RemoveData=Also delete the settings, the log and the app's SSH key?%n%nIf you are only reinstalling, choose No.
cs.RemoveData=Smazat i nastavení, záznam a SSH klíč aplikace?%n%nPokud aplikaci jen přeinstalováváš, zvol Ne.
de.RemoveData=Auch die Einstellungen, das Protokoll und den SSH-Schlüssel der App löschen?%n%nWenn du nur neu installierst, wähle Nein.
sk.RemoveData=Zmazať aj nastavenia, záznam a SSH kľúč aplikácie?%n%nAk aplikáciu len preinštalovávaš, zvoľ Nie.
pl.RemoveData=Usunąć także ustawienia, dziennik i klucz SSH aplikacji?%n%nJeśli tylko instalujesz ponownie, wybierz Nie.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion

[InstallDelete]
; Files of an older version that are no longer shipped.
Type: filesandordirs; Name: "{app}\*.pdb"

[Icons]
Name: "{autoprograms}\{cm:AppTitle}"; Filename: "{app}\{#AppExe}"
Name: "{autoprograms}\{cm:Setup}"; Filename: "{app}\{#AppExe}"; Parameters: "--setup"
Name: "{autoprograms}\{cm:Notices}"; Filename: "{app}\THIRD-PARTY-NOTICES.md"
Name: "{autodesktop}\{cm:AppTitle}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon


[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent
; Silent install started by the app's own updater: start the new version again,
; as the signed-in user rather than the elevated installer.
Filename: "{app}\{#AppExe}"; Parameters: "--updated"; Flags: nowait runasoriginaluser skipifnotsilent

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
