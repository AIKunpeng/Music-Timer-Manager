using System;

namespace MusicScheduler.Models;

/// <summary>
/// 重复规则。
/// </summary>
public enum RepeatKind
{
    Once = 0,   // 一次性（指定日期）
    Daily = 1,  // 每天
    Weekly = 2, // 每周选定的星期几
}

/// <summary>
/// 播放模式。
/// </summary>
public enum PlaybackMode
{
    Sequential = 0, // 顺序播放（播完停止，等下一次任务）
    Loop = 1,       // 循环播放（直到结束时间）
}

/// <summary>
/// 任务重复规则定义。
/// </summary>
public class RepeatRule
{
    public RepeatKind Kind { get; set; } = RepeatKind.Once;

    /// <summary>一次性任务的执行日期（仅 Once 时使用）。</summary>
    public DateTime? OnceDate { get; set; }

    /// <summary>每周重复的星期掩码（仅 Weekly 时使用）。Bit0=周日，Bit1=周一 ... Bit6=周六。</summary>
    public DayOfWeekMask DaysOfWeek { get; set; } = DayOfWeekMask.None;
}

[Flags]
public enum DayOfWeekMask
{
    None = 0,
    Sunday = 1 << 0,
    Monday = 1 << 1,
    Tuesday = 1 << 2,
    Wednesday = 1 << 3,
    Thursday = 1 << 4,
    Friday = 1 << 5,
    Saturday = 1 << 6,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekends = Saturday | Sunday,
    All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday,
}
