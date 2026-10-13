using System;
using System.Collections.Generic;

namespace MusicScheduler.Models;

/// <summary>
/// 命名播放列表（可复用的歌曲集合）。
/// </summary>
public class PlayList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新播放列表";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<TrackInfo> Tracks { get; set; } = new();

    public int TrackCount => Tracks?.Count ?? 0;
}
