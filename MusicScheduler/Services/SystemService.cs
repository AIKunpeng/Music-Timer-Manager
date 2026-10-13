using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MusicScheduler.Services;

/// <summary>
/// 系统级服务：开机自启。
/// </summary>
public static class SystemService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MusicScheduler";

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) is not null;
        }
        catch (Exception ex)
        {
            Logger.LogError($"读取自启注册表失败：{ex.Message}");
            return false;
        }
    }

    public static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key is null) return;
            if (enable)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe)) return;
                // 如果是 .dll（dotnet 启动器），需改为 exec 完整命令
                var cmd = $"\"{exe}\"";
                key.SetValue(AppName, cmd, RegistryValueKind.String);
            }
            else
            {
                if (key.GetValue(AppName) is not null)
                    key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"设置自启失败：{ex.Message}");
        }
    }
}
