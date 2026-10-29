#ifndef AppVersion
  #define AppVersion "1.0.14"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\dist\portable"
#endif
#ifndef ReleaseDir
  #define ReleaseDir "..\dist\release"
#endif
#ifndef AppIdValue
  #define AppIdValue "FzuAttendanceAssistant"
#endif
[Setup]
AppId={#AppIdValue}
AppName=晚点名助手
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher=shuiyilianmian17
AppPublisherURL=https://github.com/shuiyilianmian17/fzu-attendance-assistant
AppSupportURL=https://github.com/shuiyilianmian17/fzu-attendance-assistant/issues
AppUpdatesURL=https://github.com/shuiyilianmian17/fzu-attendance-assistant/releases
DefaultDirName={localappdata}\Programs\FzuAttendanceAssistant
DefaultGroupName=晚点名助手
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
MinVersion=10.0.18362
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=no
DisableReadyMemo=yes
UsePreviousTasks=yes
WizardStyle=modern
WizardSizePercent=110
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\晚点名助手.exe
OutputDir={#ReleaseDir}
OutputBaseFilename=FzuAttendanceAssistant-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\{#AppIdValue}.v1
UninstallLogMode=append
[Messages]
SetupAppTitle=晚点名助手
SetupWindowTitle=晚点名助手安装程序
WizardReady=准备安装
WizardInstalling=正在安装
WizardFinished=安装完成
ReadyLabel1=点击“立即安装”即可完成安装，并创建桌面快捷方式。安装完成后，请打开助手配置雷电和签到时间。
ReadyLabel2a=平时仅助手在托盘等待，到了签到时间才启动或连接模拟器。
ButtonInstall=立即安装(&I)
ButtonCancel=取消(&C)
ButtonFinish=完成(&F)
FinishedHeadingLabel=安装完成
FinishedLabel=晚点名助手已安装。首次使用需在雷电中登录智汇福大，并在助手中启用自动签到。
InstallingLabel=正在安装晚点名助手，请稍候…
SetupAppRunningError=请先从托盘退出晚点名助手，然后重试安装。
UninstallAppRunningError=请先从托盘退出晚点名助手，然后重试卸载。
[Tasks]
Name: desktopicon; Description: "创建桌面快捷方式"; Flags: checkedonce
Name: startup; Description: "登录 Windows 后自动运行助手"; Flags: checkedonce
[Files]
Source: "{#PayloadDir}\晚点名助手.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\assets\app.ico"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "{#PayloadDir}\assets\app.png"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "{#PayloadDir}\assets\app.svg"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "{#PayloadDir}\使用说明.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "installed.flag"; DestDir: "{app}"; Flags: ignoreversion; Check: not FileExists(ExpandConstant('{app}\settings.json'))
#ifndef IsolatedTest
[Icons]
Name: "{group}\晚点名助手"; Filename: "{app}\晚点名助手.exe"; IconFilename: "{app}\assets\app.ico"
Name: "{group}\卸载晚点名助手"; Filename: "{uninstallexe}"
Name: "{userdesktop}\晚点名助手"; Filename: "{app}\晚点名助手.exe"; IconFilename: "{app}\assets\app.ico"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FzuAttendanceAssistant"; ValueData: """{app}\晚点名助手.exe"" --background"; Tasks: startup
#endif
[Run]
Filename: "{app}\晚点名助手.exe"; Description: "打开晚点名助手进行首次配置"; Flags: postinstall nowait skipifsilent
[Code]
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := PageID = wpSelectTasks;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
  InstalledCommand: String;
begin
  if CurUninstallStep = usUninstall then begin
    InstalledCommand := '"' + ExpandConstant('{app}\晚点名助手.exe') + '" --background';
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FzuAttendanceAssistant', StartupCommand) then
      if CompareText(StartupCommand, InstalledCommand) = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'FzuAttendanceAssistant');
  end;
end;
