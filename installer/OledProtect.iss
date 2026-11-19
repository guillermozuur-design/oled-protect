; OLED Protect - per-user Inno Setup 6 script.
; One setup carries both payloads (x64 and x86) and picks the right one on the target machine.
; build.ps1 compiles this with /DAppVersion=<MinVer version> and /DArtifactsDir=<output folder>.

#define AppName      "OLED Protect"
#define AppExe       "OledProtect.exe"
#define RepoUrl      "https://github.com/guillermozuur-design/oled-protect"
; Named mutex held by the running app (ApplicationInstanceManager); must match the app.
#define InstanceLock "OledProtect-Mutex"
; HKCU autostart entry; the app's own "start with Windows" toggle writes the same value.
#define RunKey       "Software\Microsoft\Windows\CurrentVersion\Run"
#define UserData     "{userappdata}\OledProtect"
#define ProjectDir   "..\OledProtect"
#define StartupTask  "startup"
; Until version 3 the app was called "OLED Space Sleeper": an update moves everything over to the new name.
#define FormerName   "OLED Space Sleeper"
#define FormerLock   "OLED-Sleeper-Mutex"
#define FormerData   "{userappdata}\OLED-Space-Sleeper"

#ifndef AppVersion
  ; Only reached when compiling straight from the Inno Setup IDE.
  #define AppVersion "0.0.0-ide"
#endif
#ifndef ArtifactsDir
  #define ArtifactsDir "..\artifacts"
#endif

[Setup]
; Never change AppId, not even the doubled closing brace: existing installs are registered under exactly
; this value, and an update is only recognised when it matches.
AppId={{644E3FA9-9DF7-4CB4-B061-821BA60FC20D}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=guillermozuur-design
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile={#ProjectDir}\Assets\icon.ico
; Per-user install: no UAC prompt, {autopf} resolves to %LOCALAPPDATA%\Programs.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
; Arm64 machines take the x64 payload too; they run it under emulation.
ArchitecturesInstallIn64BitMode=x64compatible
; Setup and uninstall wait until the app has quit, so it restores monitor brightness itself first.
AppMutex={#InstanceLock},{#FormerLock}
; Install under the new name, not in the folder of an earlier "OLED Space Sleeper" install.
UsePreviousAppDir=no
UsePreviousGroup=no
ShowLanguageDialog=auto
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
OutputDir={#ArtifactsDir}
OutputBaseFilename=OledProtect-{#AppVersion}-Setup

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"

[CustomMessages]
en.AutostartTask=Start %1 automatically when I sign in to Windows
nl.AutostartTask=%1 automatisch starten als ik me aanmeld bij Windows
en.AutostartGroup=Startup:
nl.AutostartGroup=Opstarten:
en.RuntimeInstalling=Installing the .NET 8 Desktop Runtime...
nl.RuntimeInstalling=De .NET 8 Desktop Runtime wordt geïnstalleerd...
en.RuntimeCancelled=The download of the .NET 8 Desktop Runtime was cancelled. %1 needs it to run.
nl.RuntimeCancelled=Het downloaden van de .NET 8 Desktop Runtime is afgebroken. %1 heeft die nodig om te werken.
en.RuntimeFailed=The .NET 8 Desktop Runtime could not be installed (%1). Install it from https://dotnet.microsoft.com/download/dotnet/8.0 and run this setup again.
nl.RuntimeFailed=De .NET 8 Desktop Runtime kon niet worden geïnstalleerd (%1). Installeer hem via https://dotnet.microsoft.com/download/dotnet/8.0 en start deze setup daarna opnieuw.

[Tasks]
; Task names are remembered between installs; keep them stable.
Name: "{#StartupTask}"; Description: "{cm:AutostartTask,{#AppName}}"; GroupDescription: "{cm:AutostartGroup}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Settings survive an update (the file format is forward compatible). Only the remembered brightness is
; dropped: the app already put the original brightness back when setup made it quit. The wildcard also
; catches the .bak and .tmp siblings. Logs stay.
Type: files; Name: "{#UserData}\brightness_state.json*"
Type: files; Name: "{#FormerData}\brightness_state.json*"
; The program files and shortcuts of the former name (the app takes over its settings folder itself).
Type: filesandordirs; Name: "{autopf}\{#FormerName}"
Type: filesandordirs; Name: "{autoprograms}\{#FormerName}"
Type: files; Name: "{autodesktop}\{#FormerName}.lnk"

[Files]
Source: "{#ArtifactsDir}\publish\x64\*"; DestDir: "{app}"; Check: Is64BitInstallMode; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "{#ArtifactsDir}\publish\x86\*"; DestDir: "{app}"; Check: not Is64BitInstallMode; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; -h starts the app hidden in the tray.
Root: HKCU; Subkey: "{#RunKey}"; ValueName: "{#AppName}"; ValueType: string; ValueData: """{app}\{#AppExe}"" -h"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: postinstall nowait skipifsilent

[Code]
var
  RuntimeDownloadPage: TDownloadWizardPage;

{ ---------- sign-in autostart (HKCU Run values) ---------- }

// Is there a Run value with this name? Setup only ever looks at the current and the former app name.
function HasRunValue(ValueName: String): Boolean;
begin
  Result := RegValueExists(HKEY_CURRENT_USER, '{#RunKey}', ValueName);
end;

// Removes one Run value when it is there; a missing value is not an error.
procedure ClearRunValue(ValueName: String);
begin
  if not HasRunValue(ValueName) then
    Exit;
  if RegDeleteValue(HKEY_CURRENT_USER, '{#RunKey}', ValueName) then
    Log('Run value "' + ValueName + '" removed.')
  else
    Log('Run value "' + ValueName + '" could not be removed.');
end;

{ ---------- .NET 8 Desktop Runtime ---------- }

// The runtime has to match the payload we install.
function RuntimeArchitecture(): String;
begin
  if Is64BitInstallMode then
    Result := 'x64'
  else
    Result := 'x86';
end;

// The .NET installers record their folder in the 32-bit registry view; Program Files is the default.
function DotNetRoot(): String;
var
  Registered: String;
begin
  if RegQueryStringValue(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\' + RuntimeArchitecture(),
                         'InstallLocation', Registered) then
    if DirExists(Registered) then
    begin
      Result := Registered;
      Exit;
    end;
  if Is64BitInstallMode then
    Result := ExpandConstant('{commonpf64}\dotnet')
  else
    Result := ExpandConstant('{commonpf32}\dotnet');
end;

// Any 8.x Desktop Runtime is enough: the app rolls forward to the newest installed 8.0 patch.
function HasDesktopRuntime8(): Boolean;
var
  Hit: TFindRec;
begin
  Result := False;
  if not FindFirst(AddBackslash(DotNetRoot()) + 'shared\Microsoft.WindowsDesktop.App\8.*', Hit) then
    Exit;
  try
    repeat
      Result := (Hit.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0;
    until Result or not FindNext(Hit);
  finally
    FindClose(Hit);
  end;
end;

// Microsoft's aka.ms link always points at the latest 8.0 servicing release on builds.dotnet.microsoft.com.
function RuntimeDownloadUrl(): String;
begin
  Result := 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-' + RuntimeArchitecture() + '.exe';
end;

// Downloads and runs the runtime installer without UI. Returns '' when it worked, otherwise why setup
// has to stop. The runtime installer asks for elevation on its own.
function InstallDesktopRuntime(): String;
var
  LocalCopy: String;
  Code: Integer;
begin
  Result := '';
  LocalCopy := 'windowsdesktop-runtime-8-' + RuntimeArchitecture() + '.exe';
  RuntimeDownloadPage.Clear;
  RuntimeDownloadPage.Add(RuntimeDownloadUrl(), LocalCopy, '');
  RuntimeDownloadPage.Show;
  try
    try
      RuntimeDownloadPage.Download;
      RuntimeDownloadPage.SetText(CustomMessage('RuntimeInstalling'), '');
      if not ShellExec('', ExpandConstant('{tmp}\') + LocalCopy, '/install /quiet /norestart', '',
                       SW_HIDE, ewWaitUntilTerminated, Code) then
        Result := FmtMessage(CustomMessage('RuntimeFailed'), [SysErrorMessage(Code)])
      else
      begin
        Log(Format('.NET Desktop Runtime installer exit code: %d', [Code]));
        // 3010/1641: a reboot is pending, 1638: a newer build is already there. All fine for the app.
        if (Code <> 0) and (Code <> 3010) and (Code <> 1641) and (Code <> 1638) then
          Result := FmtMessage(CustomMessage('RuntimeFailed'), [IntToStr(Code)]);
      end;
    except
      if RuntimeDownloadPage.AbortedByUser then
        Result := FmtMessage(CustomMessage('RuntimeCancelled'), ['{#AppName}'])
      else
        Result := FmtMessage(CustomMessage('RuntimeFailed'), [GetExceptionMessage]);
    end;
  finally
    RuntimeDownloadPage.Hide;
  end;
end;

{ ---------- event functions ---------- }

procedure InitializeWizard;
begin
  RuntimeDownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
  // Pre-tick the startup task when this user already starts the app at sign-in, under either name
  // (set by an earlier setup or by the toggle inside the app).
  if HasRunValue('{#AppName}') or HasRunValue('{#FormerName}') then
    WizardSelectTasks('{#StartupTask}');
end;

// Runs in silent installs too, right before files are copied.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if HasDesktopRuntime8() then
    Log('.NET 8 Desktop Runtime found.')
  else
    Result := InstallDesktopRuntime();
end;

procedure CurStepChanged(Step: TSetupStep);
begin
  if Step <> ssInstall then
    Exit;
  // The former name's value starts a program this update removes; [Registry] adds the new value
  // when the task is ticked, so an unticked task has to clear it here.
  ClearRunValue('{#FormerName}');
  if not WizardIsTaskSelected('{#StartupTask}') then
    ClearRunValue('{#AppName}');
end;

procedure OfferToRemoveUserData();
var
  Folder: String;
begin
  Folder := ExpandConstant('{#UserData}');
  if not DirExists(Folder) then
    Exit;
  if MsgBox('Wil je ook je instellingen verwijderen?' + #13#10#13#10 +
            'Dat zijn je schermkeuzes, de plek van je speldje, de logboeken en de opgeslagen kaarten (' + Folder + ').' + #13#10 +
            'Kies Nee als je de app later opnieuw wilt installeren met dezelfde instellingen.',
            mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    DelTree(Folder, True, True, True);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    // uninsdeletevalue only covers a value setup wrote; the in-app toggle can create one too.
    usUninstall:
      ClearRunValue('{#AppName}');
    usPostUninstall:
      if not UninstallSilent then
        OfferToRemoveUserData();
  end;
end;
