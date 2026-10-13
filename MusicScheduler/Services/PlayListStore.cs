using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MusicScheduler.Models;

namespace MusicScheduler.Services;

/// <summary>
/// 播放列表库持久化（playlists.json 原子写入）。
/// </summary>
public class PlayListStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public string DataDir { get; }
    public string FilePath { get; protected set; }

    public PlayListStore()
    {
        DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicScheduler");
        Directory.CreateDirectory(DataDir);
        FilePath = Path.Combine(DataDir, "playlists.json");
    }

    public List<PlayList> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<PlayList>();
            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return new List<PlayList>();
            var list = JsonSerializer.Deserialize<List<PlayList>>(json, Options);
            return list ?? new List<PlayList>();
        }
        catch (Exception ex)
        {
            Logger.LogError($"读取播放列表库失败：{ex.Message}");
            return new List<PlayList>();
        }
    }

    public void Save(List<PlayList> lists)
    {
        try
        {
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(lists, Options));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }
        catch (Exception ex)
        {
            Logger.LogError($"保存播放列表库失败：{ex.Message}");
        }
    }

    /// <summary>按 Id 查找。</summary>
    public PlayList? FindById(IEnumerable<PlayList> lists, Guid id) => lists.FirstOrDefault(p => p.Id == id);

    /// <summary>按名称查找（忽略大小写）。</summary>
    public PlayList? FindByName(IEnumerable<PlayList> lists, string name) =>
        lists.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
