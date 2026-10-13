using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicScheduler.Models;
using MusicScheduler.Services;
using MusicScheduler.Views;

namespace MusicScheduler.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly TaskStore _store;
    private readonly TaskSchedulerService _scheduler;
    private readonly MusicPlaybackService _playback;

    [ObservableProperty] private string currentTimeText = string.Empty;
    [ObservableProperty] private string nextTaskText = "暂无待执行任务";
    [ObservableProperty] private string statusText = "就绪";

    public ObservableCollection<PlayTask> Tasks => _state.Tasks;
    public MusicPlaybackService Playback => _playback;
    public Models.EqualizerSettings Equalizer => _state.Equalizer;

    public MainViewModel(AppState state, TaskStore store, TaskSchedulerService scheduler, MusicPlaybackService playback)
    {
        _state = state;
        _store = store;
        _scheduler = scheduler;
        _playback = playback;

        _scheduler.TaskStarted += (t, _) =>
        {
            StatusText = $"▶ 正在播放：{t.Name}";
            ToastService.Instance.Show($"任务触发：{t.Name}", "success");
        };
        _scheduler.TaskEnded += (t, _) => StatusText = $"■ 任务结束：{t.Name}";
        _scheduler.NextTriggerChanged += OnNextTriggerChanged;

        var clock = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        clock.Tick += (_, _) => CurrentTimeText = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
        clock.Start();
        CurrentTimeText = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
    }

    [RelayCommand]
    private void NewTask()
    {
        var win = new TaskEditWindow(null);
        win.ShowDialog();
    }

    [RelayCommand]
    private void EditTask(PlayTask? task)
    {
        if (task is null) return;
        var win = new TaskEditWindow(task);
        win.ShowDialog();
    }

    [RelayCommand]
    private void DuplicateTask(PlayTask? task)
    {
        if (task is null) return;
        var copy = task.Clone();
        copy.Name = $"{task.Name}（副本）";
        Tasks.Add(copy);
        _scheduler.UpdateTasks(Tasks);
        _store.Save(Tasks.ToList());
        ToastService.Instance.Show($"已复制「{task.Name}」");
        var win = new TaskEditWindow(copy);
        win.ShowDialog();
    }

    [RelayCommand]
    private void DeleteTask(PlayTask? task)
    {
        if (task is null) return;
        var r = MessageBox.Show($"确定删除任务「{task.Name}」？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (r != MessageBoxResult.OK) return;
        Tasks.Remove(task);
        _scheduler.UpdateTasks(Tasks);
        _store.Save(Tasks.ToList());
        ToastService.Instance.Show($"已删除「{task.Name}」");
    }

    [RelayCommand]
    private void ToggleEnabled(PlayTask? task)
    {
        if (task is null) return;
        task.IsEnabled = !task.IsEnabled;
        _scheduler.UpdateTasks(Tasks);
        _store.Save(Tasks.ToList());
        ToastService.Instance.Show(task.IsEnabled ? $"已启用「{task.Name}」" : $"已停用「{task.Name}」");
    }

    [RelayCommand]
    private void RunNow(PlayTask? task)
    {
        if (task is null) return;
        if (task.Tracks.Count == 0)
        {
            ToastService.Instance.Show("该任务没有可播放的音乐。", "warning");
            return;
        }
        _playback.Start(task);
        StatusText = $"▶ 手动播放：{task.Name}";
    }

    [RelayCommand]
    private void StopPlayback() => _playback.Stop();

    [RelayCommand]
    private void OpenSettings()
    {
        new SettingsWindow().ShowDialog();
    }

    [RelayCommand]
    private void OpenAbout()
    {
        new AboutWindow().ShowDialog();
    }

    [RelayCommand]
    private void ExportTasks()
    {
        if (Tasks.Count == 0)
        {
            ToastService.Instance.Show("当前没有任务可导出。", "warning");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Title = "导出任务",
            Filter = "任务文件 (*.json)|*.json|所有文件|*.*",
            FileName = $"MusicScheduler-Tasks-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _store.Export(dlg.FileName, Tasks);
            ToastService.Instance.Show($"已导出 {Tasks.Count} 个任务到 {Path.GetFileName(dlg.FileName)}", "success");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"导出失败：{ex.Message}", "danger");
        }
    }

    [RelayCommand]
    private void ImportTasks()
    {
        var dlg = new OpenFileDialog
        {
            Title = "导入任务",
            Filter = "任务文件 (*.json)|*.json|所有文件|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var (imported, error) = _store.Import(dlg.FileName);
            if (error is not null)
            {
                ToastService.Instance.Show(error, "danger");
                return;
            }
            if (imported.Count == 0)
            {
                ToastService.Instance.Show("文件中没有可导入的任务。", "warning");
                return;
            }
            // 冲突检测：仅添加无冲突的
            int added = 0, skipped = 0;
            var skippedList = new List<string>();
            foreach (var t in imported)
            {
                var conflicts = ConflictDetector.Detect(t, Tasks, daysAhead: 7);
                if (conflicts.Count > 0)
                {
                    skipped++;
                    skippedList.Add($"{t.Name}（与 {string.Join("/", conflicts.Select(c => c.OtherTask.Name).Distinct())} 冲突）");
                }
                else
                {
                    Tasks.Add(t);
                    added++;
                }
            }
            _scheduler.UpdateTasks(Tasks);
            _store.Save(Tasks.ToList());
            // 结果汇总
            var sb = new StringBuilder();
            sb.AppendLine($"导入完成：成功 {added} 个，跳过 {skipped} 个");
            if (skippedList.Count > 0)
            {
                sb.AppendLine("跳过的任务：");
                foreach (var s in skippedList.Take(8)) sb.AppendLine($"  · {s}");
                if (skippedList.Count > 8) sb.AppendLine($"  · ……共 {skippedList.Count} 个");
            }
            MessageBox.Show(sb.ToString(), "导入结果", MessageBoxButton.OK,
                skipped == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            ToastService.Instance.Show($"导入完成：{added} 个成功，{skipped} 个跳过", added > 0 ? "success" : "warning");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"导入失败：{ex.Message}", "danger");
        }
    }

    private void OnNextTriggerChanged(PlayTask task, DateTime next)
    {
        var span = next - DateTime.Now;
        if (span.TotalSeconds < 0) span = TimeSpan.Zero;
        NextTaskText = $"下一次：{task.Name}  {next:MM-dd HH:mm:ss}（{FormatSpan(span)}后）";
    }

    private static string FormatSpan(TimeSpan span)
    {
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}天{span.Hours}时";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}时{span.Minutes}分";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}分{span.Seconds}秒";
        return $"{span.Seconds}秒";
    }
}
