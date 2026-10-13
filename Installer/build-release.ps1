# 音乐定时管家 - 一键发布脚本
# 用法：在项目根目录执行
#   powershell -ExecutionPolicy Bypass -File Installer\build-release.ps1
#
# 产物：
#   dist\green_singlefile\                        绿色版（自包含单文件 exe）
#   dist\green_multifile\                         绿色版（自包含多文件，兼容性更好）
#   dist\MusicScheduler-Green-v1.0.0.zip          绿色版压缩包
#   dist\MusicScheduler-Setup-v1.0.0.exe          安装版（需本机装有 Inno Setup 6）
#
# 安装版说明：
#   setup.iss 的 [Files] 从 MusicScheduler\bin\Release\net8.0-windows\publish\ 读取
#   因此本脚本会先把「多文件自包含」版本发布到该目录，再调用 ISCC 打包。

$ErrorActionPreference = "Stop"
$env:PATH = "C:\Program Files\dotnet;" + $env:PATH

$Root     = Split-Path -Parent $PSScriptRoot
$Project  = Join-Path $Root "MusicScheduler\MusicScheduler.csproj"
$Dist     = Join-Path $Root "dist"
$Version  = "1.0.0"
$Rid      = "win-x64"

# 安装版 payload 目录（务必与 setup.iss 的 [Files] Source 保持一致）
$InstallerPayload = Join-Path $Root "MusicScheduler\bin\Release\net8.0-windows\publish"

New-Item -ItemType Directory -Force -Path $Dist | Out-Null

# ---------- 1) 绿色版（单文件）----------
Write-Host "==> [1/4] 编译绿色版（自包含单文件）..." -ForegroundColor Cyan
$GreenSingle = Join-Path $Dist "green_singlefile"
New-Item -ItemType Directory -Force -Path $GreenSingle | Out-Null

dotnet publish $Project `
    -c Release `
    -r $Rid `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded `
    -o $GreenSingle | Out-Null

$readme = Join-Path $Root "README.md"
if (Test-Path $readme) { Copy-Item $readme (Join-Path $GreenSingle "README.md") -Force }
Write-Host "    ✓ $GreenSingle" -ForegroundColor Green

# ---------- 2) 绿色版（多文件）----------
Write-Host "==> [2/4] 编译绿色版（自包含多文件）..." -ForegroundColor Cyan
$GreenMulti = Join-Path $Dist "green_multifile"
New-Item -ItemType Directory -Force -Path $GreenMulti | Out-Null

dotnet publish $Project `
    -c Release `
    -r $Rid `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $GreenMulti | Out-Null

if (Test-Path $readme) { Copy-Item $readme (Join-Path $GreenMulti "README.md") -Force }

# 打 zip（以多文件版为内容）
$ZipPath = Join-Path $Dist "MusicScheduler-Green-v$Version.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Add-Type -AssemblyName "System.IO.Compression.FileSystem"
[System.IO.Compression.ZipFile]::CreateFromDirectory($GreenMulti, $ZipPath, "Optimal", $false)
Write-Host "    ✓ $ZipPath" -ForegroundColor Green

# ---------- 3) 安装版 payload（多文件自包含）----------
Write-Host "==> [3/4] 编译安装版 payload -> $InstallerPayload ..." -ForegroundColor Cyan
if (Test-Path $InstallerPayload) { Remove-Item $InstallerPayload -Recurse -Force }
New-Item -ItemType Directory -Force -Path $InstallerPayload | Out-Null

dotnet publish $Project `
    -c Release `
    -r $Rid `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $InstallerPayload | Out-Null

Write-Host "    ✓ 已生成 $((Get-ChildItem $InstallerPayload -Recurse -File).Count) 个文件" -ForegroundColor Green

# ---------- 4) 安装版：Inno Setup 6 编译 ----------
Write-Host "==> [4/4] 编译安装版（Inno Setup 6）..." -ForegroundColor Cyan
$isccCmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
$iscc = if ($isccCmd) { $isccCmd.Source } else { $null }
if (-not $iscc) {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { $iscc = $p; break }
    }
}
if (-not $iscc -or -not (Test-Path $iscc)) {
    Write-Host "    ! Inno Setup 6 未安装，跳过安装版生成。" -ForegroundColor Yellow
    Write-Host "      安装方式（任选其一）：" -ForegroundColor Yellow
    Write-Host "        winget install --id JRSoftware.InnoSetup -e" -ForegroundColor Yellow
    Write-Host "        或下载 https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
} else {
    $Iss = Join-Path $Root "Installer\setup.iss"
    & $iscc $Iss
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup 编译失败，退出码 $LASTEXITCODE" }
    Write-Host "    ✓ 安装版已生成" -ForegroundColor Green
}

Write-Host ""
Write-Host "发布完成！产物位于 $Dist" -ForegroundColor Green
Get-ChildItem $Dist -File | Format-Table Name, @{N='MB';E={[math]::Round($_.Length/1MB,2)}} -AutoSize
