using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicScheduler.Models;

namespace MusicScheduler.Services;

/// <summary>
/// 全局应用状态（任务列表与设置）。
/// </summary>
public partial class AppState : ObservableObject
{
    public ObservableCollection<PlayTask> Tasks { get; } = new();

    [ObservableProperty] private bool autoStart;
    [ObservableProperty] private double defaultVolume = 70;
    [ObservableProperty] private double scheduleHorizonDays = 7;
    [ObservableProperty] private double fadeSeconds = 2.0;
    [ObservableProperty] private EqualizerSettings equalizer = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicScheduler", "settings.json");

    public event Action? TasksChanged;

    /// <summary>
    /// 构造/加载期间为 true：抑制自动保存。
    /// 否则 ctor 中读取自启状态会先触发保存，用内存默认值覆盖磁盘上的用户设置。
    /// </summary>
    private bool _suppressSave;

    public AppState()
    {
        _suppressSave = true;
        // 直接写字段，避免触发 OnAutoStartChanged → SaveSettings（此时尚未 LoadSettings）
        autoStart = SystemService.IsAutoStartEnabled();
        _suppressSave = false;
        LoadSettings();
    }

    public void ReplaceTasks(IEnumerable<PlayTask> tasks)
    {
        Tasks.Clear();
        foreach (var t in tasks) Tasks.Add(t);
        TasksChanged?.Invoke();
    }

    partial void OnAutoStartChanged(bool value)
    {
        SystemService.SetAutoStart(value);
        SaveSettings();
    }

    partial void OnDefaultVolumeChanged(double value) => SaveSettings();
    partial void OnFadeSecondsChanged(double value) => SaveSettings();

    partial void OnEqualizerChanged(EqualizerSettings value)
    {
        if (value is null) return;
        // 任一频段变更后持久化
        value.PropertyChanged += (_, _) => SaveSettings();
        SaveSettings();
    }

    public void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            if (string.IsNullOrWhiteSpace(json)) return;
            var doc = JsonDocument.Parse(json);
            // 加载期间抑制保存：赋值触发的 OnXxxChanged 不应回写磁盘
            _suppressSave = true;
            try
            {
                if (doc.RootElement.TryGetProperty("DefaultVolume", out var v))
                    DefaultVolume = v.GetDouble();
                if (doc.RootElement.TryGetProperty("FadeSeconds", out var f))
                    FadeSeconds = f.GetDouble();
                if (doc.RootElement.TryGetProperty("AutoStart", out var a))
                {
                    AutoStart = a.GetBoolean();
                }
                if (doc.RootElement.TryGetProperty("Equalizer", out var eq))
                {
                    var b = eq.EnumerateObject();
                    var idx = 0;
                    foreach (var p in b)
                    {
                        if (p.Name.StartsWith("B") && p.Name.Length <= 3 && idx < 10)
                        {
                            Equalizer.SetBand(idx, (float)p.Value.GetDouble());
                            idx++;
                        }
                    }
                }
            }
            finally { _suppressSave = false; }
        }
        catch (Exception ex)
        {
            Logger.LogError($"读取设置失败：{ex.Message}");
        }
    }

    public void SaveSettings()
    {
        if (_suppressSave) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var data = new
            {
                // 音量按整数百分比落盘，避免 53.48993288590602 这类滑杆像素值
                DefaultVolume = Math.Round(DefaultVolume),
                FadeSeconds,
                AutoStart,
                Equalizer = new
                {
                    B0 = Equalizer.B0, B1 = Equalizer.B1, B2 = Equalizer.B2, B3 = Equalizer.B3, B4 = Equalizer.B4,
                    B5 = Equalizer.B5, B6 = Equalizer.B6, B7 = Equalizer.B7, B8 = Equalizer.B8, B9 = Equalizer.B9,
                    Preset = Equalizer.Preset
                }
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, Options));
        }
        catch (Exception ex)
        {
            Logger.LogError($"保存设置失败：{ex.Message}");
        }
    }
}
