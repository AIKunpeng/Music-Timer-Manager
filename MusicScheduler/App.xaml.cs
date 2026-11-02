using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MusicScheduler.Services;
using MusicScheduler.ViewModels;
using MusicScheduler.Views;

namespace MusicScheduler;

public partial class App : Application
{
    public static System.Collections.Generic.Dictionary<Type, object> Services { get; } = new();

    private static readonly string StartupLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicScheduler", "startup.log");

    private Mutex? _mutex;
    private TaskStore? _store;
    private AppState? _state;
    private TaskSchedulerService? _scheduler;
    private MusicPlaybackService? _playback;
    private PlayListStore? _playListStore;

    public static T GetService<T>() where T : class
    {
        if (Services.TryGetValue(typeof(T), out var svc)) return (T)svc;
        throw new InvalidOperationException($"Service not registered: {typeof(T).Name}");
    }

    private static void Log(string msg)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StartupLogPath)!);
            File.AppendAllText(StartupLogPath,
                $"{DateTime.Now:O} [{Environment.ProcessId}] {msg}\n");
        }
        catch { /* 日志失败不影响主流程 */ }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // 0) 最早一行：写时间戳到固定日志（用于定位"连进程都没有"类故障）
        Log("=== 启动 OnStartup ===");
        Log($"exe: {Environment.ProcessPath}");
        Log($"cwd: {Environment.CurrentDirectory}");
        Log($"os: {Environment.OSVersion}");
        Log($"args: {string.Join(' ', e.Args)}");

        // 0.1) 注册全局 try-catch（捕获 .NET 运行时加载之后的所有崩溃）
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log($"[FATAL] AppDomain UnhandledException: {args.ExceptionObject}");
        };
        DispatcherUnhandledException += (_, args) =>
        {
            Log($"[FATAL] Dispatcher UnhandledException: {args.Exception}");
            MessageBox.Show($"发生异常：{args.Exception.Message}\n\n详细请查看 startup.log",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // 1) 单实例互斥锁
        bool created;
        try
        {
            _mutex = new Mutex(true, "MusicScheduler.SingleInstance", out created);
        }
        catch (Exception ex)
        {
            Log($"[FATAL] Mutex 创建失败：{ex}");
            MessageBox.Show($"互斥锁创建失败：{ex.Message}", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (!created)
        {
            Log("已检测到另一个实例，退出");
            MessageBox.Show("音乐定时管家已经在运行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        Log("互斥锁创建成功");

        base.OnStartup(e);

        // 2) 初始化服务
        try
        {
            _store = new TaskStore();
            Log($"TaskStore 初始化完成，路径: {_store.FilePath}");
        }
        catch (Exception ex) { Log($"[FATAL] TaskStore 初始化失败: {ex}"); throw; }

        try
        {
            _state = new AppState();
            Log("AppState 初始化完成");
        }
        catch (Exception ex) { Log($"[FATAL] AppState 初始化失败: {ex}"); throw; }

        try
        {
            _playback = new MusicPlaybackService();
            _playback.BindEqualizer(_state.Equalizer);
            // 启动音量取设置页的"默认音量"（%）
            _playback.Volume = (float)Math.Clamp(_state.DefaultVolume / 100.0, 0f, 1f);
            Log("MusicPlaybackService 初始化完成");
        }
        catch (Exception ex) { Log($"[FATAL] MusicPlaybackService 初始化失败: {ex}"); throw; }

        try
        {
            _scheduler = new TaskSchedulerService(_playback, _state);
            Log("TaskSchedulerService 初始化完成");
        }
        catch (Exception ex) { Log($"[FATAL] TaskSchedulerService 初始化失败: {ex}"); throw; }

        try
        {
            _playListStore = new PlayListStore();
            Log("PlayListStore 初始化完成");
        }
        catch (Exception ex) { Log($"[FATAL] PlayListStore 初始化失败: {ex}"); throw; }

        // 3) 加载任务
        var tasks = _store.Load();
        Log($"加载任务: {tasks.Count} 个");
        _state.ReplaceTasks(tasks);
        _scheduler.UpdateTasks(_state.Tasks);
        _scheduler.Start();
        Log("调度器已启动");

        // 4) 系统唤醒事件（重新校准调度）
        SystemEvents_Wire();

        // 5) 注册到服务容器
        Services[typeof(TaskStore)] = _store;
        Services[typeof(AppState)] = _state;
        Services[typeof(TaskSchedulerService)] = _scheduler;
        Services[typeof(MusicPlaybackService)] = _playback;
        Services[typeof(PlayListStore)] = _playListStore;

        // 6) 创建主窗口
        try
        {
            var mainVm = new MainViewModel(_state, _store, _scheduler, _playback);
            var main = new MainWindow(mainVm);
            MainWindow = main;
            main.Show();
            Log("主窗口已创建并显示");
        }
        catch (Exception ex)
        {
            Log($"[FATAL] 主窗口创建失败: {ex}");
            MessageBox.Show($"主窗口创建失败：{ex.Message}\n\n{ex.StackTrace}", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        Log("=== 启动完成 ===");
    }

    private void SystemEvents_Wire()
    {
        try
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged += (_, args) =>
            {
                if (args.Mode == Microsoft.Win32.PowerModes.Resume)
                {
                    Logger.Log("系统唤醒，重新校准调度");
                    Log("系统唤醒，重新校准调度");
                    _scheduler?.RecalculateAll();
                }
            };
            Log("系统事件已注册");
        }
        catch (Exception ex)
        {
            Logger.LogError($"注册系统事件失败：{ex.Message}");
            Log($"注册系统事件失败：{ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log($"=== 应用退出 (ExitCode={e.ApplicationExitCode}) ===");
        try
        {
            _playback?.Stop();
            _scheduler?.Stop();
            _scheduler?.Dispose();
            _mutex?.ReleaseMutex();
        }
        catch { }
        base.OnExit(e);
    }
}
