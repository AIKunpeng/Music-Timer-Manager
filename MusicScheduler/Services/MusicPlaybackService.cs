using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicScheduler.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicScheduler.Services;

/// <summary>
/// 音乐播放服务。封装 NAudio，使用 MediaFoundationReader 解码（系统原生支持 MP3/AAC/FLAC）。
/// 单实例：同时只播放一个任务。
///
/// 信号链：MediaFoundationReader → SampleProvider → FadeInOutSampleProvider → EqualizerSampleProvider → VolumeSampleProvider → WasapiOut
/// </summary>
public partial class MusicPlaybackService : ObservableObject
{
    private readonly object _lock = new();
    private IWavePlayer? _player;
    private WaveStream? _reader;
    private FadeInOutSampleProvider? _fade;
    private EqualizerSampleProvider? _eq;
    private VolumeSampleProvider? _volume;
    private PlayTask? _activeTask;
    private PlayTask? _lastTask; // 保留最近一次播放的任务，停止后仍可点播放重新开始
    private int _currentIndex;
    private EqualizerSettings? _eqSettings;
    /// <summary>任务专属 EQ 切换前的全局 EQ 快照（包含 10 段增益 + 预设名），用于停止后还原。</summary>
    private EqualizerSettings? _eqSnapshot;
    private float _volumeValue = 0.7f;
    private CancellationTokenSource? _trackMonitorCts;

    [ObservableProperty] private bool isPlaying;
    [ObservableProperty] private bool isPaused;
    [ObservableProperty] private string currentTitle = "（未播放）";
    [ObservableProperty] private string currentTaskName = string.Empty;
    [ObservableProperty] private double currentPositionSeconds;
    [ObservableProperty] private double currentDurationSeconds;
    [ObservableProperty] private double volumePercent = 70;
    [ObservableProperty] private bool canSeek;

    private string _currentTrackTitle = string.Empty;

    /// <summary>UI 正在拖动进度条期间为 true，此时后台不回推位置，避免拖动被覆盖。</summary>
    public volatile bool IsSeeking;

    public ICommand TogglePauseCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }

    public MusicPlaybackService()
    {
        TogglePauseCommand = new RelayCommand(() => TogglePause());
        PreviousCommand = new RelayCommand(() => Previous());
        NextCommand = new RelayCommand(() => Next());
    }

    public PlayTask? ActiveTask
    {
        get { lock (_lock) return _activeTask; }
    }

    public event EventHandler? PlaybackStopped;

    public float Volume
    {
        get => _volumeValue;
        set
        {
            _volumeValue = Math.Clamp(value, 0f, 1f);
            VolumePercent = _volumeValue * 100;
            lock (_lock)
            {
                if (_volume is not null)
                {
                    try { _volume.Volume = _volumeValue; } catch { }
                }
            }
        }
    }

    /// <summary>绑定均衡器配置（外部设置变更后调用 ApplyEqualizer 立即生效）。</summary>
    public void BindEqualizer(EqualizerSettings settings)
    {
        _eqSettings = settings;
        if (settings is not null)
        {
            settings.PropertyChanged -= OnEqPropertyChanged;
            settings.PropertyChanged += OnEqPropertyChanged;
        }
        ApplyEqualizer();
    }

    private void OnEqPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ApplyEqualizer();
    }

    private void ApplyEqualizer()
    {
        lock (_lock)
        {
            if (_eq is not null && _eqSettings is not null)
            {
                _eq.Update(_eqSettings);
            }
        }
    }

    /// <summary>开始播放任务。在结束时间到达时应外部调用 Stop。</summary>
    public bool Start(PlayTask task)
    {
        lock (_lock)
        {
            StopInternal();
            // 若任务指定了 EQ 预设，先快照当前全局 EQ，再切到任务专属设置
            ApplyTaskEqualizer(task);
            _activeTask = task;
            _lastTask = task;
            _currentIndex = 0;
            CurrentTaskName = task.Name;
            return PlayCurrent();
        }
    }

    /// <summary>任务启动时应用任务专属 EQ 预设（带快照以便停止时还原）。</summary>
    private void ApplyTaskEqualizer(PlayTask task)
    {
        if (_eqSettings is null) return;
        if (string.IsNullOrEmpty(task.EqPreset)) return;

        // 1) 快照当前全局 EQ（含 10 段 + 预设名）
        _eqSnapshot = new EqualizerSettings { Preset = _eqSettings.Preset };
        for (int i = 0; i < EqualizerSettings.BandCount; i++)
            _eqSnapshot.SetBand(i, _eqSettings.GetBand(i));

        // 2) 切换到任务专属预设（mutate _eqSettings → 触发 OnEqPropertyChanged → ApplyEqualizer）
        _eqSettings.ApplyPreset(task.EqPreset);
        Logger.Log($"任务「{task.Name}」应用 EQ 预设：{task.EqPreset}（停止时还原）");
    }

    /// <summary>任务结束/停止时还原全局 EQ（若之前切换过）。</summary>
    private void RestoreGlobalEqualizer()
    {
        if (_eqSnapshot is null || _eqSettings is null) return;
        for (int i = 0; i < EqualizerSettings.BandCount; i++)
            _eqSettings.SetBand(i, _eqSnapshot.GetBand(i));
        _eqSettings.Preset = _eqSnapshot.Preset;
        Logger.Log($"已还原全局 EQ：{_eqSnapshot.Preset}");
        _eqSnapshot = null;
    }

    public void Stop()
    {
        lock (_lock)
        {
            StopInternal();
        }
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>启动淡出（在结束时间前调用，seconds 秒后音量为 0）。</summary>
    public void BeginFadeOut(double seconds)
    {
        lock (_lock)
        {
            if (_fade is not null)
            {
                try { _fade.BeginFadeOut(seconds); } catch { }
            }
        }
    }

    /// <summary>启动淡入（任务开始时调用，seconds 秒内音量由 0 升至当前音量）。</summary>
    public void BeginFadeIn(double seconds)
    {
        lock (_lock)
        {
            if (_fade is null) return;
            try
            {
                _fade.BeginFadeIn(seconds);
            }
            catch { }
        }
    }

    public void TogglePause()
    {
        lock (_lock)
        {
            try
            {
                if (_player is null)
                {
                    // 当前没有活动播放器：优先恢复活动任务，否则重新播放最近一次的任务
                    var task = _activeTask ?? _lastTask;
                    if (task is not null && task.Tracks.Count > 0)
                    {
                        _activeTask = task;
                        _lastTask = task;
                        _currentIndex = 0; // 重新播放从头开始
                        if (string.IsNullOrEmpty(CurrentTaskName)) CurrentTaskName = task.Name;
                        PlayCurrent();
                    }
                    return;
                }

                if (_player.PlaybackState == PlaybackState.Playing)
                {
                    _player.Pause();
                    IsPlaying = false;
                    IsPaused = true;
                    CurrentTitle = $"{_currentTrackTitle}（已暂停）";
                    Logger.Log($"暂停播放：{_currentTrackTitle}");
                }
                else if (_player.PlaybackState == PlaybackState.Paused)
                {
                    _player.Play();
                    IsPlaying = true;
                    IsPaused = false;
                    CurrentTitle = _currentTrackTitle;
                    Logger.Log($"继续播放：{_currentTrackTitle}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"暂停切换失败：{ex.Message}");
            }
        }
    }

    /// <summary>跳转到指定秒数（进度条拖拽快进/后退）。</summary>
    public bool Seek(double seconds)
    {
        lock (_lock)
        {
            if (_reader is null) return false;
            try
            {
                var dur = _reader.TotalTime.TotalSeconds;
                if (dur <= 0) return false;
                seconds = Math.Clamp(seconds, 0, Math.Max(0, dur - 0.2));
                _reader.CurrentTime = TimeSpan.FromSeconds(seconds);
                CurrentPositionSeconds = seconds;
                Logger.Log($"跳转播放进度：{TimeSpan.FromSeconds(seconds):hh\\:mm\\:ss}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"跳转失败：{ex.Message}");
                return false;
            }
        }
    }

    /// <summary>UI 拖动进度条期间仅刷新位置显示（不实际跳转）。</summary>
    public void UpdatePositionDisplay(double seconds) => CurrentPositionSeconds = seconds;

    public void Next()
    {
        lock (_lock)
        {
            if (_activeTask is null || _activeTask.Tracks.Count == 0) return;
            StopInternal(raiseEvent: false);
            _currentIndex = (_currentIndex + 1) % _activeTask.Tracks.Count;
            PlayCurrent();
        }
    }

    public void Previous()
    {
        lock (_lock)
        {
            if (_activeTask is null || _activeTask.Tracks.Count == 0) return;
            StopInternal(raiseEvent: false);
            _currentIndex = (_currentIndex - 1 + _activeTask.Tracks.Count) % _activeTask.Tracks.Count;
            PlayCurrent();
        }
    }

    private bool PlayCurrent()
    {
        if (_activeTask is null) return false;

        while (_currentIndex < _activeTask.Tracks.Count)
        {
            var track = _activeTask.Tracks[_currentIndex];
            if (!track.Exists)
            {
                Logger.LogError($"文件不存在，跳过：{track.FilePath}");
                _currentIndex++;
                continue;
            }

            try
            {
                _reader = new MediaFoundationReader(track.FilePath);
                // 构造信号链
                var sample = _reader.ToSampleProvider();
                _fade = new FadeInOutSampleProvider(sample);
                _eq = new EqualizerSampleProvider(_fade);
                if (_eqSettings is not null) _eq.Update(_eqSettings);
                _volume = new VolumeSampleProvider(_eq) { Volume = _volumeValue };

                _player = new WasapiOut(AudioClientShareMode.Shared, 0);
                _player.PlaybackStopped += OnPlaybackStoppedInternal;
                _player.Init(_volume);
                _player.Play();
                IsPlaying = true;
                IsPaused = false;
                _currentTrackTitle = track.DisplayTitle;
                CurrentTitle = _currentTrackTitle;
                CurrentDurationSeconds = _reader.TotalTime.TotalSeconds;
                CurrentPositionSeconds = 0;
                CanSeek = true;
                StartTrackMonitor();
                Logger.Log($"开始播放：{track.DisplayTitle}（任务：{_activeTask.Name}）");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"播放失败 {track.FilePath}：{ex.Message}");
                StopInternal(raiseEvent: false);
                _currentIndex++;
            }
        }

        CurrentTitle = "（无可用音乐）";
        IsPlaying = false;
        IsPaused = false;
        _currentTrackTitle = string.Empty;
        return false;
    }

    private void StartTrackMonitor()
    {
        _trackMonitorCts?.Cancel();
        _trackMonitorCts = new CancellationTokenSource();
        var token = _trackMonitorCts.Token;
        System.Threading.Tasks.Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(500, token);
                }
                catch (OperationCanceledException) { return; }
                catch { return; }

                lock (_lock)
                {
                    if (_reader is null) return;
                    if (IsSeeking) continue; // 用户拖动进度条期间不回推位置
                    try
                    {
                        CurrentPositionSeconds = _reader.CurrentTime.TotalSeconds;
                    }
                    catch { }
                }
            }
        }, token);
    }

    private void OnPlaybackStoppedInternal(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            if (_activeTask is null) return;

            if (_activeTask.Mode == PlaybackMode.Loop)
            {
                StopInternal(raiseEvent: false);
                _currentIndex = (_currentIndex + 1) % Math.Max(1, _activeTask.Tracks.Count);
                PlayCurrent();
            }
            else
            {
                StopInternal(raiseEvent: false);
                _currentIndex++;
                if (_currentIndex < _activeTask.Tracks.Count)
                {
                    PlayCurrent();
                }
                else
                {
                    CurrentTitle = "（已结束）";
                    IsPlaying = false;
                    IsPaused = false;
                    PlaybackStopped?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    private void StopInternal(bool raiseEvent = true)
    {
        _trackMonitorCts?.Cancel();
        _trackMonitorCts = null;

        if (_player is not null)
        {
            try
            {
                _player.PlaybackStopped -= OnPlaybackStoppedInternal;
                _player.Stop();
            }
            catch { }
            finally
            {
                _player.Dispose();
                _player = null;
            }
        }

        if (_reader is not null)
        {
            try { _reader.Dispose(); } catch { }
            _reader = null;
        }

        _fade = null;
        _eq = null;
        _volume = null;

        IsPlaying = false;
        IsPaused = false;
        CanSeek = false;
        IsSeeking = false;
        _currentTrackTitle = string.Empty;

        // 还原任务开始前快照的全局 EQ（仅在任务指定过 EqPreset 时才生效）
        RestoreGlobalEqualizer();

        if (raiseEvent)
        {
            _activeTask = null;
            CurrentTaskName = string.Empty;
        }
    }
}
