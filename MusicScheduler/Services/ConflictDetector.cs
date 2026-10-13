using System;
using System.Collections.Generic;
using System.Linq;
using MusicScheduler.Models;

namespace MusicScheduler.Services;

/// <summary>
/// 任务时间冲突检测器。
/// </summary>
public static class ConflictDetector
{
    /// <summary>将任务展开为指定起止日期内的具体时间区间列表（用于冲突检测）。</summary>
    public static List<TimeSlot> Expand(PlayTask task, DateTime from, DateTime to)
    {
        var result = new List<TimeSlot>();
        if (task.Tracks is null || task.Tracks.Count == 0) return result;
        if (task.EndTime <= task.StartTime) return result;

        var day = from.Date;
        while (day <= to.Date)
        {
            if (TaskAppliesOn(task, day))
            {
                var start = day + task.StartTime;
                var end = day + task.EndTime;
                if (end > from && start < to)
                {
                    result.Add(new TimeSlot(start, end));
                }
            }
            day = day.AddDays(1);
        }
        return result;
    }

    /// <summary>
    /// 检测任务与现有任务集合在指定时间窗内是否有冲突。
    /// 返回冲突详情列表。
    /// </summary>
    public static List<ConflictInfo> Detect(PlayTask candidate, IEnumerable<PlayTask> existing, int daysAhead = 7)
    {
        var conflicts = new List<ConflictInfo>();
        if (candidate is null) return conflicts;
        var now = DateTime.Now;
        var horizon = now.AddDays(daysAhead);

        var candidateSlots = Expand(candidate, now, horizon);
        if (candidateSlots.Count == 0) return conflicts;

        foreach (var other in existing)
        {
            if (other is null) continue;
            if (other.Id == candidate.Id) continue;
            if (!other.IsEnabled) continue;

            var otherSlots = Expand(other, now, horizon);
            foreach (var cs in candidateSlots)
            {
                foreach (var os in otherSlots)
                {
                    if (cs.Overlaps(os))
                    {
                        var overlapStart = cs.Start > os.Start ? cs.Start : os.Start;
                        var overlapEnd = cs.End < os.End ? cs.End : os.End;
                        conflicts.Add(new ConflictInfo
                        {
                            OtherTask = other,
                            OverlapStart = overlapStart,
                            OverlapEnd = overlapEnd
                        });
                        break; // 一个其他任务只记录一次
                    }
                }
                if (conflicts.Any(c => c.OtherTask.Id == other.Id)) break;
            }
        }
        return conflicts;
    }

    private static bool TaskAppliesOn(PlayTask task, DateTime day)
    {
        return task.Repeat.Kind switch
        {
            RepeatKind.Once => task.Repeat.OnceDate?.Date == day,
            RepeatKind.Daily => true,
            RepeatKind.Weekly => MatchesWeekly(task.Repeat.DaysOfWeek, day.DayOfWeek),
            _ => false
        };
    }

    private static bool MatchesWeekly(DayOfWeekMask mask, DayOfWeek dow)
    {
        var bit = (DayOfWeekMask)(1 << ((int)dow));
        return (mask & bit) != 0;
    }
}

public class ConflictInfo
{
    public PlayTask OtherTask { get; set; } = null!;
    public DateTime OverlapStart { get; set; }
    public DateTime OverlapEnd { get; set; }
    public string Description => $"与任务「{OtherTask.Name}」冲突：{OverlapStart:yyyy-MM-dd HH:mm:ss} ~ {OverlapEnd:HH:mm:ss}";
}
