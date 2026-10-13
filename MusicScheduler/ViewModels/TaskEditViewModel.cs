using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicScheduler.Models;
using MusicScheduler.Services;

namespace MusicScheduler.ViewModels;

public partial class TaskEditViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly TaskStore _store;
    private readonly TaskSchedulerService _scheduler;
    private readonly PlayListStore _playlistStore;
    private readonly PlayTask _editing;
    private readonly bool _isNew;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private TimeSpan startTime;
    [ObservableProperty] private TimeSpan endTime;
    [ObservableProperty] private int repeatKindIndex;
    [ObservableProperty] private DateTime? onceDate;
    [ObservableProperty] private DateTime weekAnchor = DateTime.Today;
    [ObservableProperty] private bool sunday;
    [ObservableProperty] private bool monday;
    [ObservableProperty] private bool tuesday;
    [ObservableProperty] private bool wednesday;
    [ObservableProperty] private bool thursday;
    [ObservableProperty] private bool friday;
    [ObservableProperty] private bool saturday;
    [ObservableProperty] private int playbackModeIndex;
    [ObservableProperty] private bool isEnabled;
    [ObservableProperty] private int eqPresetIndex; // 0 = 跟随全局，1..N = 索引 EqualizerSettings.AvailablePresets

    /// <summary>提供给下拉框的数据源（"跟随全局" + 8 个预设）。</summary>
    public IReadOnlyList<string> EqPresetOptions { get; } = new[] { "跟随全局" }
        .Concat(EqualizerSettings.AvailablePresets)
        .ToList();
    [ObservableProperty] private string conflictMessage = string.Empty;
    [ObservableProperty] private bool hasConflict;

    public ObservableCollection<TrackInfo> Tracks { get; } = new();
    public ObservableCollection<PlayList> AvailablePlayLists { get; } = new();

    public string WindowTitle => _isNew ? "新建播放任务" : $"编辑任务 - {_editing.Name}";

    public TaskEditViewModel(PlayTask? existing, AppState state, TaskStore store, TaskSchedulerService scheduler, PlayListStore playlistStore)
    {
        _state = state;
        _store = store;
        _scheduler = scheduler;
        _playlistStore = playlistStore;
        if (existing is null)
        {
            _isNew = true;
            _editing = new PlayTask
            {
                Name = "新任务",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                Repeat = new RepeatRule { Kind = RepeatKind.Daily },
                IsEnabled = true
            };
        }
        else
        {
            _isNew = false;
            _editing = existing;
            foreach (var t in existing.Tracks) Tracks.Add(t);
        }

        name = _editing.Name;
        startTime = _editing.StartTime;
        endTime = _editing.EndTime;
        repeatKindIndex = (int)_editing.Repeat.Kind;
        onceDate = _editing.Repeat.OnceDate ?? DateTime.Today;
        var days = _editing.Repeat.DaysOfWeek;
        sunday = (days & DayOfWeekMask.Sunday) != 0;
        monday = (days & DayOfWeekMask.Monday) != 0;
        tuesday = (days & DayOfWeekMask.Tuesday) != 0;
        wednesday = (days & DayOfWeekMask.Wednesday) != 0;
        thursday = (days & DayOfWeekMask.Thursday) != 0;
        friday = (days & DayOfWeekMask.Friday) != 0;
        saturday = (days & DayOfWeekMask.Saturday) != 0;
        playbackModeIndex = (int)_editing.Mode;
        isEnabled = _editing.IsEnabled;
        // 0 = 跟随全局；>0 时索引到 AvailablePresets
        eqPresetIndex = string.IsNullOrEmpty(_editing.EqPreset)
            ? 0
            : Math.Max(1, Array.IndexOf(EqualizerSettings.AvailablePresets, _editing.EqPreset) + 1);
        if (eqPresetIndex < 0 || eqPresetIndex >= EqPresetOptions.Count) eqPresetIndex = 0;

        LoadPlayLists();
    }

    private void LoadPlayLists()
    {
        try
        {
            AvailablePlayLists.Clear();
            foreach (var p in _playlistStore.Load()) AvailablePlayLists.Add(p);
        }
        catch { }
    }

    [RelayCommand]
    private void AddFiles()
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "音频文件|*.mp3;*.aac;*.m4a;*.flac;*.wav;*.wma|所有文件|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            foreach (var f in dlg.FileNames)
            {
                if (Tracks.Any(t => string.Equals(t.FilePath, f, StringComparison.OrdinalIgnoreCase))) continue;
                Tracks.Add(new TrackInfo { FilePath = f });
            }
        }
    }

    [RelayCommand]
    private void RemoveTrack(TrackInfo? track)
    {
        if (track is null) return;
        Tracks.Remove(track);
    }

    [RelayCommand]
    private void MoveUp(TrackInfo? track)
    {
        if (track is null) return;
        var idx = Tracks.IndexOf(track);
        if (idx > 0) Tracks.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveDown(TrackInfo? track)
    {
        if (track is null) return;
        var idx = Tracks.IndexOf(track);
        if (idx >= 0 && idx < Tracks.Count - 1) Tracks.Move(idx, idx + 1);
    }

    /// <summary>从播放列表库中插入选中列表的所有歌曲到当前任务。</summary>
    [RelayCommand]
    private void InsertFromPlayList(PlayList? list)
    {
        if (list is null) return;
        int added = 0;
        foreach (var t in list.Tracks)
        {
            if (Tracks.Any(x => string.Equals(x.FilePath, t.FilePath, StringComparison.OrdinalIgnoreCase))) continue;
            Tracks.Add(new TrackInfo { FilePath = t.FilePath, Title = t.Title });
            added++;
        }
        ToastService.Instance.Show($"从「{list.Name}」插入了 {added} 首歌曲", added > 0 ? "success" : "warning");
    }

    /// <summary>将当前任务歌曲另存为命名播放列表。</summary>
    [RelayCommand]
    private void SaveAsPlayList()
    {
        if (Tracks.Count == 0)
        {
            ToastService.Instance.Show("当前任务没有歌曲，无法保存为播放列表。", "warning");
            return;
        }
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "为播放列表命名：", "保存为播放列表", string.IsNullOrWhiteSpace(Name) ? "我的播放列表" : Name);
        if (string.IsNullOrWhiteSpace(input)) return;
        try
        {
            var lists = _playlistStore.Load();
            // 同名覆盖
            var existing = lists.FirstOrDefault(p => string.Equals(p.Name, input, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Tracks = Tracks.Select(t => new TrackInfo { FilePath = t.FilePath, Title = t.Title }).ToList();
            }
            else
            {
                lists.Add(new PlayList
                {
                    Name = input.Trim(),
                    Tracks = Tracks.Select(t => new TrackInfo { FilePath = t.FilePath, Title = t.Title }).ToList()
                });
            }
            _playlistStore.Save(lists);
            LoadPlayLists();
            ToastService.Instance.Show($"已保存「{input}」（{Tracks.Count} 首）", "success");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"保存失败：{ex.Message}", "danger");
        }
    }

    [RelayCommand]
    private void DeletePlayList(PlayList? list)
    {
        if (list is null) return;
        var r = MessageBox.Show($"确定删除播放列表「{list.Name}」？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (r != MessageBoxResult.OK) return;
        try
        {
            var lists = _playlistStore.Load();
            lists.RemoveAll(p => p.Id == list.Id);
            _playlistStore.Save(lists);
            AvailablePlayLists.Remove(list);
            ToastService.Instance.Show($"已删除「{list.Name}」");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"删除失败：{ex.Message}", "danger");
        }
    }

    public bool SaveAndClose(Window window)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("请输入任务名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (EndTime <= StartTime)
        {
            MessageBox.Show("结束时间必须大于开始时间。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (Tracks.Count == 0)
        {
            MessageBox.Show("请至少添加一首音乐。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (RepeatKindIndex == (int)RepeatKind.Weekly && !HasAnyWeekDay())
        {
            MessageBox.Show("每周重复需至少选择一天。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _editing.Name = Name.Trim();
        _editing.StartTime = StartTime;
        _editing.EndTime = EndTime;
        _editing.Repeat = new RepeatRule
        {
            Kind = (RepeatKind)RepeatKindIndex,
            OnceDate = RepeatKindIndex == (int)RepeatKind.Once ? OnceDate?.Date : null,
            DaysOfWeek = BuildWeekMask()
        };
        _editing.Mode = (PlaybackMode)PlaybackModeIndex;
        _editing.IsEnabled = IsEnabled;
        _editing.EqPreset = EqPresetIndex <= 0 ? null : EqPresetOptions[EqPresetIndex];
        _editing.Tracks = Tracks.ToList();

        var conflicts = ConflictDetector.Detect(_editing, _state.Tasks, daysAhead: 7);
        if (conflicts.Count > 0)
        {
            HasConflict = true;
            ConflictMessage = string.Join("\n", conflicts.Select(c => c.Description));
            return false;
        }

        if (_isNew)
        {
            _state.Tasks.Add(_editing);
        }
        _scheduler.UpdateTasks(_state.Tasks);
        _store.Save(_state.Tasks.ToList());

        window.DialogResult = true;
        window.Close();
        return true;
    }

    private bool HasAnyWeekDay() => Sunday || Monday || Tuesday || Wednesday || Thursday || Friday || Saturday;

    private DayOfWeekMask BuildWeekMask()
    {
        DayOfWeekMask m = DayOfWeekMask.None;
        if (Sunday) m |= DayOfWeekMask.Sunday;
        if (Monday) m |= DayOfWeekMask.Monday;
        if (Tuesday) m |= DayOfWeekMask.Tuesday;
        if (Wednesday) m |= DayOfWeekMask.Wednesday;
        if (Thursday) m |= DayOfWeekMask.Thursday;
        if (Friday) m |= DayOfWeekMask.Friday;
        if (Saturday) m |= DayOfWeekMask.Saturday;
        return m;
    }
}
