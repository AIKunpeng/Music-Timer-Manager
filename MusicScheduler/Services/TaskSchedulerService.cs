using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MusicScheduler.Models;

namespace MusicScheduler.Services;

/// <summary>
/// 自研秒级调度引擎（不依赖 Windows 计划任务）。
/// </summary>
public class TaskSchedulerService : IDisposable
{
    private readonly MusicPlaybackService _playback;
    private readonly AppState _state;
    private readonly System.Timers.Timer _timer;
    private readonly object _lock = new();
    private List<PlayTask> _tasks = new();
    private PlayTask? _currentRunningTask;
    private DateTime? _currentEndAt;
    private bool _fadeOutTriggered;
    private readonly HashSet<Guid> _triggeredToday = new();
    private DateTime _lastTickDay = DateTime.MinValue;

    public event Action<PlayTask, DateTime>? TaskStarted;
    public event Action<PlayTask, DateTime>? TaskEnded;
    public event Action<PlayTask, DateTime>? NextTriggerChanged;

    public IReadOnlyList<PlayTask> Tasks
    {
        get { lock (_lock) return _tasks.ToList(); }
    }

    public TaskSchedulerService(MusicPlaybackService playback, AppState state)
    {
        _playback = playback;
        _state = state;
        _timer = new System.Timers.Timer(500);
        _timer.Elapsed += OnTick;
        _timer.AutoReset = true;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    public void UpdateTasks(IEnumerable<PlayTask> tasks)
    {
        lock (_lock)
        {
            _tasks = tasks.ToList();
            _triggeredToday.Clear();
            _lastTickDay = DateTime.MinValue;
        }
    }

    public void RecalculateAll()
    {
        lock (_lock)
        {
            _triggeredToday.Clear();
            _lastTickDay = DateTime.MinValue;
        }
    }

    public PlayTask? GetNextPendingTask()
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            return _tasks
                .Where(t => t.IsEnabled && NextTrigger(t, now) is not null)
                .OrderBy(t => NextTrigger(t, now)!)
                .FirstOrDefault();
        }
    }

    public DateTime? NextTrigger(PlayTask task, DateTime now)
    {
        if (!task.IsEnabled) return null;
        if (task.Tracks is null || task.Tracks.Count == 0) return null;
        if (task.EndTime <= task.StartTime) return null;

        for (int dayOffset = 0; dayOffset < 8; dayOffset++)
        {
            var day = now.Date.AddDays(dayOffset);
            if (!AppliesOn(task, day)) continue;
            var start = day + task.StartTime;
            if (start < now) continue;
            return start;
        }
        return null;
    }

    private static bool AppliesOn(PlayTask task, DateTime day)
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

    private void OnTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var now = DateTime.Now;
        lock (_lock)
        {
            if (_lastTickDay != now.Date)
            {
                _lastTickDay = now.Date;
                _triggeredToday.Clear();
            }

            // 1) 任务结束处理：先在结束前 N 秒淡出，到点强制停止
            if (_currentRunningTask is not null && _currentEndAt is not null)
            {
                var fadeSeconds = Math.Max(0, _state.FadeSeconds);
                if (!_fadeOutTriggered && fadeSeconds > 0 && now >= _currentEndAt.Value.AddSeconds(-fadeSeconds))
                {
                    _playback.BeginFadeOut(fadeSeconds);
                    _fadeOutTriggered = true;
                    Logger.Log($"任务 {_currentRunningTask.Name} 开始淡出（{fadeSeconds:F1}s）");
                }
                if (now >= _currentEndAt.Value)
                {
                    var ended = _currentRunningTask;
                    _playback.Stop();
                    _currentRunningTask = null;
                    _currentEndAt = null;
                    _fadeOutTriggered = false;
                    Logger.Log($"任务结束：{ended.Name}");
                    TaskEnded?.Invoke(ended, now);
                }
            }

            // 2) 到点触发
            foreach (var task in _tasks)
            {
                if (!task.IsEnabled) continue;
                if (_triggeredToday.Contains(task.Id)) continue;
                if (!AppliesOn(task, now.Date)) continue;
                if (task.Tracks.Count == 0) continue;

                var triggerStart = now.Date + task.StartTime;
                if (now >= triggerStart && (now - triggerStart).TotalSeconds < 1.5)
                {
                    if (_currentRunningTask is not null)
                    {
                        Logger.LogError($"兜底：{task.Name} 触发时仍有任务 {_currentRunningTask.Name}，强制停止");
                        var old = _currentRunningTask;
                        _playback.Stop();
                        TaskEnded?.Invoke(old, now);
                    }
                    _playback.Start(task);
                    _currentRunningTask = task;
                    _currentEndAt = now.Date + task.EndTime;
                    _fadeOutTriggered = false;
                    _triggeredToday.Add(task.Id);
                    if (task.Repeat.Kind == RepeatKind.Once) task.HasRun = true;
                    Logger.Log($"任务开始：{task.Name}，将在 {_currentEndAt:HH:mm:ss} 结束");
                    TaskStarted?.Invoke(task, now);

                    // 任务开始淡入
                    var fs = Math.Max(0, _state.FadeSeconds);
                    if (fs > 0) _playback.BeginFadeIn(fs);
                    break;
                }
            }

            // 3) 倒计时
            var next = GetNextPendingTask();
            if (next is not null)
            {
                var nt = NextTrigger(next, now);
                if (nt is not null) NextTriggerChanged?.Invoke(next, nt.Value);
            }
        }
    }

    public void Dispose() => _timer.Dispose();
}
