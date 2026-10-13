using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicScheduler.Models;

/// <summary>
/// 播放任务定义（实现 INotifyPropertyChanged，使编辑后卡片实时刷新）。
/// </summary>
public partial class PlayTask : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    private Guid _id = Guid.NewGuid();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatDisplay))]
    [NotifyPropertyChangedFor(nameof(TimeRangeDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private string _name = "新任务";

    /// <summary>开始时间（精确到秒）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeRangeDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private TimeSpan _startTime = new TimeSpan(9, 0, 0);

    /// <summary>结束时间（精确到秒）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeRangeDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private TimeSpan _endTime = new TimeSpan(10, 0, 0);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private RepeatRule _repeat = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatDisplay))]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private PlaybackMode _mode = PlaybackMode.Sequential;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private List<TrackInfo> _tracks = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private bool _isEnabled = true;

    /// <summary>
    /// 任务专属均衡预设（null = 跟随全局设置）。可选值见 <see cref="EqualizerSettings.AvailablePresets"/>。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EqPresetDisplay))]
    private string? _eqPreset;

    /// <summary>任务卡片上显示的均衡模式标记（如"🎚 流行"），未设置时返回空字符串。</summary>
    [JsonIgnore]
    public string EqPresetDisplay => string.IsNullOrEmpty(EqPreset) ? string.Empty : $"🎚 {EqPreset}";

    /// <summary>用于一次性任务标记完成。</summary>
    [ObservableProperty]
    [JsonIgnore]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private bool _hasRun;

    public string RepeatDisplay
    {
        get
        {
            return Repeat.Kind switch
            {
                RepeatKind.Once => Repeat.OnceDate is null
                    ? "一次性"
                    : $"一次性 {Repeat.OnceDate:yyyy-MM-dd}",
                RepeatKind.Daily => "每天",
                RepeatKind.Weekly => "每周 " + DaysOfWeekText(Repeat.DaysOfWeek),
                _ => "未知"
            };
        }
    }

    public string TimeRangeDisplay => $"{StartTime:hh\\:mm\\:ss} - {EndTime:hh\\:mm\\:ss}";

    /// <summary>深拷贝当前任务（新 Id、新重复规则、复制曲目列表，HasRun 重置）。</summary>
    public PlayTask Clone()
    {
        return new PlayTask
        {
            Name = Name,
            StartTime = StartTime,
            EndTime = EndTime,
            Repeat = new RepeatRule
            {
                Kind = Repeat.Kind,
                OnceDate = Repeat.OnceDate,
                DaysOfWeek = Repeat.DaysOfWeek
            },
            Mode = Mode,
            IsEnabled = IsEnabled,
            EqPreset = EqPreset,
            HasRun = false,
            Tracks = Tracks.Select(t => new TrackInfo
            {
                FilePath = t.FilePath,
                Title = t.Title,
                DurationSeconds = t.DurationSeconds
            }).ToList()
        };
    }

    public string StatusDisplay => IsEnabled ? (HasRun ? "已完成" : "运行中") : "已停用";

    private static string DaysOfWeekText(DayOfWeekMask mask)
    {
        if (mask == DayOfWeekMask.None) return "未选";
        if (mask == DayOfWeekMask.All) return "每天";
        var names = new System.Text.StringBuilder();
        string[] arr = { "日", "一", "二", "三", "四", "五", "六" };
        for (int i = 0; i < 7; i++)
        {
            var d = (DayOfWeekMask)(1 << i);
            if ((mask & d) != 0)
            {
                if (names.Length > 0) names.Append(' ');
                names.Append(arr[i]);
            }
        }
        return names.ToString();
    }
}
