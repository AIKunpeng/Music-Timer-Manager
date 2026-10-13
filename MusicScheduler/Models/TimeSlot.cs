using System;

namespace MusicScheduler.Models;

/// <summary>
/// 表示一个具体的时间区间，用于冲突检测。
/// </summary>
public readonly record struct TimeSlot(DateTime Start, DateTime End)
{
    public bool Overlaps(TimeSlot other)
    {
        return Start < other.End && other.Start < End;
    }
}
