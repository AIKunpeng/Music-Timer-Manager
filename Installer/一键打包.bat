@echo off
chcp 65001 >nul
title 音乐定时管家 - 一键打包
cd /d "%~dp0.."

rem ── 确保 dotnet 可用（未加入 PATH 时使用默认安装位置）──
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
