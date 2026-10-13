using System;
using System.IO;
using System.Text;

namespace MusicScheduler.Services;

/// <summary>
/// 简易文件日志。
/// </summary>
public static class Logger
{
    private static readonly object Lock = new();
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicScheduler");
    private static readonly string LogPath = Path.Combine(LogDir, "log.txt");

    public static void Log(string message)
    {
        Write("INFO", message);
        System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
    }

    public static void LogError(string message)
    {
        Write("ERROR", message);
        System.Diagnostics.Debug.WriteLine($"[ERROR] {message}");
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(LogDir);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
