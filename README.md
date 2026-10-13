# 音乐定时管家

> **MIT License** | Windows 桌面音乐定时播放软件

一款 Windows 桌面音乐定时控制软件，支持 MP3 / AAC / FLAC 等常见格式，按用户预设的任务在精确到秒的时间点自动开始/停止播放音乐，**不依赖 Windows 计划任务程序**。

## 核心特性

- 🎵 **多格式音频**：基于系统 Media Foundation 原生解码，支持 MP3 / AAC / M4A / FLAC / WAV / WMA
- ⏰ **秒级精度的自研调度引擎**：500ms 周期扫描，到点自动播放/结束，无需 Windows 计划任务
- 📋 **多任务管理**：每个任务支持单曲或多曲列表（顺序/循环播放）；一次性 / 每天 / 每周任意星期
- ⚠️ **时间冲突检测**：保存任务时自动展开未来 7 天的所有时间区间，冲突时拒绝保存并明确提示
- 🚀 **开机自启**：一键开启，注销 HKCU 注册表实现，无需管理员权限
- 🪟 **系统托盘常驻**：关闭窗口自动最小化到托盘，托盘菜单快速恢复/停止/退出
- 🛌 **睡眠唤醒校准**：系统从睡眠恢复后自动重新校准所有任务的下一次触发时间
- 🔒 **单实例保护**：防止重复启动导致重复调度

## 技术栈

- .NET 8 + WPF（C#）
- [NAudio](https://github.com/naudio/NAudio) — 音频播放（MediaFoundationReader + WasapiOut）
- [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) — 系统托盘
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM

## 目录结构

```
桌面音乐控制软件/
├── MusicScheduler.sln
├── MusicScheduler/                  主项目
│   ├── App.xaml / App.xaml.cs       应用入口 + DI 容器 + 单实例
│   ├── AssemblyInfo.cs              程序集版权信息
│   ├── Models/                      数据模型
│   │   ├── PlayTask.cs              播放任务
│   │   ├── RepeatRule.cs            重复规则 + 星期掩码
│   │   ├── TrackInfo.cs             歌曲条目
│   │   └── TimeSlot.cs              时间区间（冲突检测）
│   ├── Services/                    服务层
│   │   ├── TaskSchedulerService.cs  自研秒级调度引擎
│   │   ├── MusicPlaybackService.cs  NAudio 播放封装
│   │   ├── TaskStore.cs             JSON 持久化（原子写入）
│   │   ├── ConflictDetector.cs      时间冲突检测
│   │   ├── SystemService.cs         开机自启（HKCU 注册表）
│   │   ├── AppState.cs              全局状态
│   │   └── Logger.cs                简易文件日志
│   ├── ViewModels/                  MVVM 视图模型
│   ├── Views/                       窗口（Main/TaskEdit/Settings/About）
│   ├── Styles/                      XAML 样式（深色雅致主题）
│   ├── Converters/                  XAML 值转换器
│   └── Assets/                      图标
└── Tests/                           控制台测试（核心逻辑）
```

## 使用方法

1. 启动应用，主窗口左侧为播放器面板，右侧为任务列表
2. 点击「+ 新建任务」设置：
   - 任务名称
   - 开始/结束时间（精确到秒，格式 `HH:MM:SS`）
   - 重复规则（一次性 / 每天 / 每周）
   - 播放列表（添加 MP3/AAC/FLAC 等文件）
   - 播放模式（顺序 / 循环）
3. 保存时若与其他任务时间冲突，会以红色提示框列出冲突详情，**拒绝保存**
4. 关闭窗口自动最小化到系统托盘，定时任务继续在后台运行
5. 托盘菜单：打开主界面 / 停止播放 / 关于 / 退出
6. 顶栏「⚙ 设置」中可一键启用/关闭开机自启

## 编译运行

```powershell
# 开发运行
cd d:/Develop/桌面音乐控制软件
dotnet run --project MusicScheduler

# 核心逻辑测试
cd Tests && dotnet run
```

## 发布版本

项目发布到 `dist/` 目录，提供两种格式供选择：

| 目录 | 形态 | 体积 | 启动可靠性 | 推荐度 |
|---|---|---|---|---|
| `dist\green_multifile\` | 多文件绿色版（exe + 150 个 dll） | ~200 MB（多文件） | **高**（无解压步骤） | ⭐⭐⭐ 推荐 |
| `dist\green_singlefile\` | 单文件自包含 exe | ~155 MB（单文件） | 中（首次启动需解压到 %TEMP%） | ⭐⭐ |

### 多文件绿色版（推荐）
```powershell
dotnet publish MusicScheduler -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "dist\green_multifile"
```
启动 `dist\green_multifile\MusicScheduler.exe` 即可。**不依赖任何运行时**，复制整个目录即可分发。

### 单文件版
```powershell
dotnet publish MusicScheduler -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "dist\green_singlefile"
```
只有一个 exe，方便分发。但首次启动需解压到 `%TEMP%\.net\MusicScheduler`，可能受杀软/权限影响。

## 故障排查

### 双击 exe 无反应
按以下顺序排查：

1. **使用多文件版**：直接使用 `dist\green_multifile\`（`MusicScheduler.exe` + 150 个 dll），可绕过单文件解压失败问题
2. **查看启动日志**：每次启动都会在 `%APPDATA%\MusicScheduler\startup.log` 写详细步骤，能定位崩溃点
3. **解除网络下载标记**（解压自 zip 时会带上）：
   ```powershell
   Unblock-File .\MusicScheduler.exe
   ```
4. **退出残留进程**：
   ```powershell
   Get-Process MusicScheduler -ErrorAction SilentlyContinue | Stop-Process -Force
   ```
5. **从命令行启动看错误**：
   ```powershell
   .\MusicScheduler.exe 1>out.log 2>err.log; "ExitCode: $LASTEXITCODE"
   Get-Content out.log, err.log
   ```
6. **Defender 误报**：159MB 自包含 exe 极易被 360/火绒/Defender 静默拦截，加入白名单即可

### 日志位置
- 启动日志：`%APPDATA%\MusicScheduler\startup.log`（每次启动追加）
- 业务日志：`%APPDATA%\MusicScheduler\log.txt`

## 数据存储

- 任务数据：`%APPDATA%\MusicScheduler\tasks.json`
- 启动日志：`%APPDATA%\MusicScheduler\startup.log`
- 运行日志：`%APPDATA%\MusicScheduler\log.txt`
- 开机自启：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\MusicScheduler`

## 版权声明

```
版权所有 © 2026 深圳安信联创科技有限公司
```

## License

[MIT](LICENSE)
