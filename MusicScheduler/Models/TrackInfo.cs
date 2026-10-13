using System;
using System.IO;

namespace MusicScheduler.Models;

/// <summary>
/// 歌曲条目信息。
/// </summary>
public class TrackInfo
{
    public string FilePath { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>时长（秒）。如无法读取则为 0。</summary>
    public double DurationSeconds { get; set; }

    public bool Exists => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    public string DisplayTitle => string.IsNullOrEmpty(Title) ? Path.GetFileNameWithoutExtension(FilePath) : Title;

    public string DisplayDuration
    {
        get
        {
            if (DurationSeconds <= 0) return "--:--";
            var ts = TimeSpan.FromSeconds(DurationSeconds);
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
        }
    }
}
