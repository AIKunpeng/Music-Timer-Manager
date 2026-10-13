; 音乐定时管家 - Inno Setup 6 安装脚本
; 版权所有 (c) 深圳安信联创科技有限公司

#define MyAppName "音乐定时管家"
#define MyAppNameEn "MusicScheduler"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "深圳安信联创科技有限公司"
#define MyAppURL "https://example.com/musicscheduler"
#define MyAppExeName "MusicScheduler.exe"
#define MyAppCopyright "版权所有 (c) 深圳安信联创科技有限公司"

[Setup]
AppId={{B6F9F5C2-8C3F-4F6B-9D7E-2A1B5C6D7E8F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppCopyright={#MyAppCopyright}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\README.md
OutputDir=..\dist
OutputBaseFilename=MusicScheduler-Setup-v{#MyAppVersion}
SetupIconFile=..\MusicScheduler\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoDescription={#MyAppName} 安装程序
MinVersion=10.0

[Languages]
; Inno Setup 6 官方不再内置简体中文，使用随仓库附带的翻译文件（同目录）
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Messages]
WelcomeLabel2=欢迎使用 [name/ver] 安装程序。%n%n本程序由 深圳安信联创科技有限公司 开发，用于按预设时间自动播放音乐。%n%n接下来将把程序安装到您的电脑上。

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:";
Name: "autostart"; Description: "开机自动启动"; GroupDescription: "附加任务:";

[Files]
Source: "..\MusicScheduler\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppNameEn}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即运行 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; 卸载时清理自启注册表项
Filename: "reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""{#MyAppNameEn}"" /f"; Flags: runhidden; RunOnceId: "CleanupAutostart"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\tasks.json"

[Code]
// 启动时检测已有自启项，安装时自动勾选
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

function ShouldAddAutoStartTask(): Boolean;
var
  RegValue: string;
begin
  Result := False;
  if RegQueryStringValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#MyAppNameEn}', RegValue) then
    Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // 安装后动作
end;
