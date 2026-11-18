@echo off
rem 一键打包：必须与 build-release.ps1 放在同一目录（Installer），双击运行即可
rem 注意：本文件必须保存为 ANSI/GBK 编码，中文 Windows 的 cmd 默认代码页 936 才能正确解析
title 音乐定时管家 - 一键打包

rem ── 定位项目根目录（本文件在 Installer\ 下）──
cd /d "%~dp0.."
if errorlevel 1 (
    echo [错误] 无法定位项目根目录。请勿把本文件单独复制到桌面等位置运行，
    echo        请直接双击 D:\Develop\桌面音乐控制软件\Installer\一键打包.bat
    pause
    exit /b 1
)

rem ── 校验打包脚本是否存在 ──
if not exist "%~dp0build-release.ps1" (
    echo [错误] 找不到打包脚本 build-release.ps1
    echo        一键打包.bat 必须与 build-release.ps1 位于同一目录 Installer 下
    pause
    exit /b 1
)

rem ── 校验 .NET SDK ──
if not exist "%ProgramFiles%\dotnet\dotnet.exe" (
    echo [错误] 未找到 .NET SDK，请先安装 .NET 8 SDK
    echo        下载: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
set "PATH=%ProgramFiles%\dotnet;%PATH%"

echo ==============================================
echo   音乐定时管家 一键打包（4 类产物）
echo   [1/4] 绿色版-单文件  [2/4] 绿色版+zip
echo   [3/4] 安装版 payload [4/4] 安装版 Setup
echo ==============================================
echo.
echo 预计耗时 2-4 分钟，请勿关闭本窗口...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1"
set "RC=%ERRORLEVEL%"

echo.
if "%RC%"=="0" (
    echo [完成] 打包成功，产物见 dist 目录：
    dir /b /o-d dist\*.exe dist\*.zip 2>nul
) else (
    echo [失败] 打包脚本退出码 %RC%，请向上滚动查看具体错误
)
echo.
pause
exit /b %RC%
