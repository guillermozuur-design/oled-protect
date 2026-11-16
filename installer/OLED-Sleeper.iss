; Inno Setup installer script for OLED Space Sleeper (a fork of OLED Sleeper by Quorthon13)
; Builds a single installer supporting both x64 and x86 deployments.

#include "CodeDependencies.iss"

; The build script passes the MinVer-derived version as /DAppVersion=. This fallback only applies to a
; compile started from the Inno Setup IDE.
#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif

[Setup]
; Unique application identifier used by Windows for installation tracking.
AppId={{644E3FA9-9DF7-4CB4-B061-821BA60FC20D}}

; Application metadata shown in the installer and system UI.
AppName=OLED Space Sleeper
UninstallDisplayName=OLED Space Sleeper
AppVersion={#AppVersion}
AppPublisher=guillermozuur-design
AppPublisherURL=https://github.com/guillermozuur-design/oled-space-sleeper
AppSupportURL=https://github.com/guillermozuur-design/oled-space-sleeper/issues

; Installation runs without elevation.
PrivilegesRequired=lowest

; The mutex ApplicationInstanceManager holds. Setup and uninstall both wait for the application to be closed,
; so its own shutdown restores monitor brightness before any file is replaced or removed.
AppMutex=OLED-Sleeper-Mutex

; Output installer configuration.
OutputBaseFilename=OLED-Space-Sleeper-{#AppVersion}-Setup
SourceDir=.
OutputDir=..\artifacts

; Default installation directory.
DefaultDirName={autopf}\OLED Space Sleeper

; Enables 64-bit installation mode when running on x64 systems.
ArchitecturesInstallIn64BitMode=x64compatible

; Installer and uninstall entry icons.
SetupIconFile=..\OLED-Sleeper\Assets\icon.ico
UninstallDisplayIcon={app}\OLED-Sleeper.exe

; General installer UI and compression settings.
DefaultGroupName=OLED Space Sleeper
AllowNoIcons=yes
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
; Default language configuration.
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Optional installation tasks presented to the user.
Name: "startup"; Description: "Launch OLED Space Sleeper when Windows starts"; GroupDescription: "Additional options:";
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional shortcuts:";

[InstallDelete]
; Settings are KEPT on install and update: the settings file stays compatible between versions (schema 1;
; new options get their default). Only a recorded brightness is cleared — setup waits for the app to close,
; and the app restores brightness itself when it closes. Logs are kept as well.
Type: files; Name: "{userappdata}\OLED-Space-Sleeper\brightness_state.json"
Type: files; Name: "{userappdata}\OLED-Space-Sleeper\brightness_state.json.bak"
Type: files; Name: "{userappdata}\OLED-Space-Sleeper\brightness_state.json.tmp"

[Files]
; Install platform-specific binaries based on system architecture.
Source: "..\artifacts\publish\x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: Is64BitInstallMode
Source: "..\artifacts\publish\x86\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: not Is64BitInstallMode

[Icons]
; Start Menu and optional Desktop shortcuts.
Name: "{group}\OLED Space Sleeper"; Filename: "{app}\OLED-Sleeper.exe"
Name: "{autodesktop}\OLED Space Sleeper"; Filename: "{app}\OLED-Sleeper.exe"; Tasks: desktopicon

[Registry]
; Optional autostart entry created when the startup task is selected.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OLED Space Sleeper"; ValueData: """{app}\OLED-Sleeper.exe"" -h"; Flags: uninsdeletevalue; Tasks: startup

[Run]
; Optionally launch the application after installation completes.
Filename: "{app}\OLED-Sleeper.exe"; Description: "{cm:LaunchProgram,OLED Space Sleeper}"; Flags: nowait postinstall skipifsilent

[Code]

// Reports whether the application's autostart registry entry exists.
function StartupIsEnabled(): Boolean;
begin
  Result := RegValueExists(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'OLED Space Sleeper');
end;

// Ticks the startup task when autostart is already enabled, whether it was enabled here or from the
// application's own toggle. Unlisted tasks keep their state.
procedure InitializeWizard();
begin
  if StartupIsEnabled() then
  begin
    WizardSelectTasks('startup');
  end;
end;

// Removes the application's autostart registry entry if present.
procedure RemoveStartupKey();
begin
  if RegValueExists(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'OLED Space Sleeper') then
  begin
    Log('Removing startup registry key.');
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'OLED Space Sleeper');
  end;
end;

// Clears the autostart entry during installation only when the startup task was left unticked. The
// [Registry] entry writes it back when the task is ticked.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssInstall) and (not WizardIsTaskSelected('startup')) then
  begin
    RemoveStartupKey();
  end;
end;

// Runs during uninstallation to ensure the autostart entry is removed.
procedure CurUninstallStepChanged(UninstallStep: TUninstallStep);
begin
  if UninstallStep = usUninstall then
  begin
    RemoveStartupKey();
  end;
end;

// Requests download of required runtime dependencies before installation begins.
function InitializeSetup(): Boolean;
begin
  Dependency_AddDotNet80Desktop;
  Result := True;
end;