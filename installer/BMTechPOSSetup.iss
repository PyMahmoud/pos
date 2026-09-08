; BMTech POS -- Inno Setup script
;
; Packages the Release build (staged by .github/workflows/build-demo.yml's
; "build" job into dist\) into a single Windows installer .exe. Built by
; the workflow's "installer" job on a windows-latest runner -- Baraa's dev
; machine is Linux and can't run Inno Setup / ISCC directly, so this is
; meant to be compiled by CI, not locally, same reasoning as the rest of
; this solution needing Windows/Mahmoud for anything requiring an actual
; Windows build.
;
; Local Windows build: if Mahmoud ever wants to build this by hand instead
; of via CI, build the solution in Release first (bin\Release\*), copy that
; folder's contents into dist\ next to this installer\ folder (i.e. dist\
; and installer\ as siblings under the repo root), then run:
;   iscc installer\BMTechPOSSetup.iss
; from the repo root. The compiled installer lands in installer\Output\.
;
; AppId is a fixed GUID (not the app name) -- Inno Setup uses it to
; recognize "this is the same product" across versions so upgrades install
; over the previous copy instead of side-by-side. Never change this once
; it's shipped to the client; only AppVersion below should change release
; to release.
#define MyAppId "{8F1C1E1A-5B2D-4E3F-9C6A-2E7F0B6D9A11}"
#define MyAppName "BMTech POS"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "BMTech"
#define MyAppExeName "PosSystem.App.exe"

[Setup]
AppId={{#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; No client-supplied icon file exists yet (see PosSystem.App\Assets --
; only IconGeometries.cs, vector path data for in-app XAML icons, not a
; standalone .ico). Drop a real .ico at installer\BMTechPOS.ico and
; uncomment the two lines below once one exists.
;SetupIconFile=BMTechPOS.ico
;UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=BMTechPOS-Setup
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
; .NET Framework 4.8 target (PosSystem.App.csproj) -- most Windows 10/11
; machines already have 4.8 or later; this installer doesn't bundle or
; check for the runtime. If a client machine is missing it, the app will
; fail to launch with a .NET-related error rather than this installer
; catching it up front -- worth adding a runtime-check task later if that
; ever comes up in the field (see Server.cs's "works under Wine, fails on
; Windows 10" open issue for the kind of environment gap this could catch).
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Everything staged into dist\ by the build job (PosSystem.App.exe, its
; DLLs, rovaShop.db, the x86\/x64\ SQLite.Interop.dll folders, etc.) --
; recursesubdirs+createallsubdirs to carry those native-DLL subfolders
; over correctly rather than flattening them.
Source: "..\dist\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
