using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicScheduler.Models;

/// <summary>
/// 10 段图形均衡器配置（中心频率：31/62/125/250/500/1k/2k/4k/8k/16k Hz，-12~+12 dB）。
/// </summary>
public partial class EqualizerSettings : ObservableObject
{
    public const int BandCount = 10;
    public const float MinDb = -12f;
    public const float MaxDb = 12f;

    /// <summary>各频段中心频率 (Hz)。</summary>
    public static readonly float[] CenterFrequencies = { 31f, 62f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };

    [ObservableProperty] private float b0 = 0f;
    [ObservableProperty] private float b1 = 0f;
    [ObservableProperty] private float b2 = 0f;
    [ObservableProperty] private float b3 = 0f;
    [ObservableProperty] private float b4 = 0f;
    [ObservableProperty] private float b5 = 0f;
    [ObservableProperty] private float b6 = 0f;
    [ObservableProperty] private float b7 = 0f;
    [ObservableProperty] private float b8 = 0f;
    [ObservableProperty] private float b9 = 0f;

    [ObservableProperty] private string preset = "平坦";

    public float GetBand(int index) => index switch
    {
        0 => B0, 1 => B1, 2 => B2, 3 => B3, 4 => B4,
        5 => B5, 6 => B6, 7 => B7, 8 => B8, 9 => B9,
        _ => 0f
    };

    public void SetBand(int index, float db)
    {
        var clamped = Math.Clamp(db, MinDb, MaxDb);
        switch (index)
        {
            case 0: B0 = clamped; break;
            case 1: B1 = clamped; break;
            case 2: B2 = clamped; break;
            case 3: B3 = clamped; break;
            case 4: B4 = clamped; break;
            case 5: B5 = clamped; break;
            case 6: B6 = clamped; break;
            case 7: B7 = clamped; break;
            case 8: B8 = clamped; break;
            case 9: B9 = clamped; break;
        }
    }

    public void Reset()
    {
        for (int i = 0; i < BandCount; i++) SetBand(i, 0f);
        Preset = "平坦";
    }

    /// <summary>应用预设（数值参考常见均衡器预设）。</summary>
    public void ApplyPreset(string name)
    {
        float[] gains = name switch
        {
            "流行" => new float[] { 2, 3, 1, 0, -1, -1, 0, 2, 3, 3 },
            "古典" => new float[] { 4, 3, 2, 1, 0, 0, -1, -1, -2, -3 },
            "摇滚" => new float[] { 5, 4, 2, 0, -2, -2, 0, 3, 4, 5 },
            "人声" => new float[] { -2, -1, 0, 2, 4, 4, 2, 0, -1, -2 },
            "电子" => new float[] { 4, 3, 0, 0, -2, 2, 0, 1, 3, 5 },
            "低音增强" => new float[] { 6, 5, 3, 1, 0, 0, 0, 0, 0, 0 },
            "高音增强" => new float[] { 0, 0, 0, 0, 0, 0, 1, 3, 5, 6 },
            _ => new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
        };
        for (int i = 0; i < BandCount; i++) SetBand(i, gains[i]);
        Preset = name;
    }

    public static string[] AvailablePresets { get; } = { "平坦", "流行", "古典", "摇滚", "人声", "电子", "低音增强", "高音增强" };
}
