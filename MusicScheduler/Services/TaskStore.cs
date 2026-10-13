using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MusicScheduler.Models;

namespace MusicScheduler.Services;

/// <summary>
/// 任务持久化（JSON 原子写入）。
/// </summary>
public class TaskStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public string DataDir { get; }
    public string FilePath { get; protected set; }

    public TaskStore()
    {
        DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicScheduler");
        Directory.CreateDirectory(DataDir);
        FilePath = Path.Combine(DataDir, "tasks.json");
    }

    public List<PlayTask> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<PlayTask>();
            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return new List<PlayTask>();
            var list = JsonSerializer.Deserialize<List<PlayTask>>(json, Options);
            return list ?? new List<PlayTask>();
        }
        catch (Exception ex)
        {
            Logger.LogError($"读取任务失败：{ex.Message}");
            return new List<PlayTask>();
        }
    }

    public void Save(List<PlayTask> tasks)
    {
        try
        {
            var tmp = FilePath + ".tmp";
            var json = JsonSerializer.Serialize(tasks, Options);
            File.WriteAllText(tmp, json);
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }
        catch (Exception ex)
        {
            Logger.LogError($"保存任务失败：{ex.Message}");
        }
    }

    /// <summary>导出任务列表到指定文件（与 AppData 同样的 JSON 格式）。</summary>
    public void Export(string path, IEnumerable<PlayTask> tasks)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(tasks.ToList(), Options);
        File.WriteAllText(path, json);
    }

    /// <summary>从指定文件导入任务。返回 (导入成功列表, 失败原因)。</summary>
    public (List<PlayTask> tasks, string? error) Import(string path)
    {
        try
        {
            if (!File.Exists(path)) return (new List<PlayTask>(), $"文件不存在：{path}");
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return (new List<PlayTask>(), "文件内容为空");
            var list = JsonSerializer.Deserialize<List<PlayTask>>(json, Options);
            if (list is null) return (new List<PlayTask>(), "解析失败：文件不是有效 JSON");
            // 为新导入的任务分配新 Id，避免与现有冲突
            foreach (var t in list) t.Id = Guid.NewGuid();
            return (list, null);
        }
        catch (Exception ex)
        {
            return (new List<PlayTask>(), $"解析失败：{ex.Message}");
        }
    }
}
