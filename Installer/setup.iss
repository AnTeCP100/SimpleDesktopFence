; SimpleDesktopFence - Inno Setup Script


#define AppName      "SimpleDesktopFence"
#define AppVersion   "1.0.0"
#define AppPublisher "Jacky"
#define AppURL       "https://github.com/AnTeCP100/SimpleDesktopFence"
#define AppExeName   "SimpleDesktopFence.exe"

#define PublishDir "C:\Users\jacky\Desktop\SideProject\SimpleDesktopFence\SimpleDesktopFence\bin\Release\net8.0-windows\publish\win-x64"

[Setup]
AppId={{B7E3F1A2-C849-4D56-9E0F-12AB34CD5678}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes

; 安裝包輸出位置（桌面，方便找到）
OutputDir=C:\Users\jacky\Desktop
OutputBaseFilename=SimpleDesktopFence_Setup_{#AppVersion}

; 壓縮設定
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; 需要 Windows 10 以上
MinVersion=10.0

; 64 位元專用
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; 現代安裝精靈外觀
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "建立桌面捷徑"; GroupDescription: "額外選項:"; Flags: unchecked

[Files]
; 複製 publish 資料夾內所有檔案到安裝目錄
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; 開始選單捷徑
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExeName}"
Name: "{group}\解除安裝 {#AppName}";    Filename: "{uninstallexe}"

; 桌面捷徑（勾選才建立）
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; 安裝完成後詢問是否立即啟動
Filename: "{app}\{#AppExeName}"; Description: "立即啟動 {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; 解除安裝前先關閉程式，避免檔案被鎖定
Filename: "taskkill.exe"; Parameters: "/f /im {#AppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[Code]
// 升級或安裝前，先關閉正在執行的舊版本
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
    Exec('taskkill.exe', '/f /im {#AppExeName}', '', SW_HIDE,
         ewWaitUntilTerminated, ResultCode);
end;
