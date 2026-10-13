using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MusicScheduler.Models;
using MusicScheduler.Services;

int passed = 0, failed = 0;
void Assert(string name, bool cond)
{
    if (cond) { passed++; Console.WriteLine($"  ✓ {name}"); }
    else { failed++; Console.WriteLine($"  ✗ {name}"); }
}

Console.WriteLine("=== ConflictDetector 测试 ===");
var daily9to10 = new PlayTask
{
    Id = Guid.NewGuid(), Name = "daily9to10",
    StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0),
    Repeat = new RepeatRule { Kind = RepeatKind.Daily },
    Tracks = new List<TrackInfo> { new() { FilePath = "a.mp3" } }
};
var c1 = ConflictDetector.Detect(new PlayTask
{
    Id = Guid.NewGuid(), Name = "10:30~11:30",
    StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(11, 30, 0),
    Repeat = new RepeatRule { Kind = RepeatKind.Daily },
    Tracks = new List<TrackInfo> { new() { FilePath = "b.mp3" } }
}, new[] { daily9to10 });
Assert("10:30~11:30 与 09:00~10:00 无冲突", c1.Count == 0);

var c2 = ConflictDetector.Detect(new PlayTask
{
    Id = Guid.NewGuid(), Name = "9:30~10:30",
    StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(10, 30, 0),
    Repeat = new RepeatRule { Kind = RepeatKind.Daily },
    Tracks = new List<TrackInfo> { new() { FilePath = "c.mp3" } }
}, new[] { daily9to10 });
Assert("9:30~10:30 与 09:00~10:00 冲突", c2.Count == 1);

Console.WriteLine("\n=== EqualizerSettings 测试 ===");
var eq = new EqualizerSettings();
Assert("新均衡器所有频段为 0", Enumerable.Range(0, 10).All(i => eq.GetBand(i) == 0f));
Assert("新均衡器预设为 平坦", eq.Preset == "平坦");

eq.ApplyPreset("流行");
Assert("流行预设 B0=2", eq.GetBand(0) == 2f);
Assert("流行预设 B9=3", eq.GetBand(9) == 3f);
Assert("流行预设 Preset=流行", eq.Preset == "流行");

eq.Reset();
Assert("Reset 后所有频段为 0", Enumerable.Range(0, 10).All(i => eq.GetBand(i) == 0f));
Assert("Reset 后 Preset=平坦", eq.Preset == "平坦");

eq.SetBand(5, 8.5f);
Assert("SetBand 5 = 8.5", eq.GetBand(5) == 8.5f);
eq.SetBand(7, 20f); // 超出范围
Assert("SetBand 越界被 clamp 到 12", eq.GetBand(7) == 12f);
eq.SetBand(3, -20f);
Assert("SetBand 越下界被 clamp 到 -12", eq.GetBand(3) == -12f);

Console.WriteLine("\n=== PlayListStore 持久化往返测试 ===");
var tempDir = Path.Combine(Path.GetTempPath(), "MusicSchedulerTest_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
try
{
    var storePath = Path.Combine(tempDir, "playlists.json");
    var store = new TestPlayListStore(storePath);
    var lists = new List<PlayList>
    {
        new() { Name = "晨间", Tracks = new() { new() { FilePath = "m1.mp3" }, new() { FilePath = "m2.flac" } } },
        new() { Name = "运动", Tracks = new() { new() { FilePath = "s.mp3" } } }
    };
    store.Save(lists);
    Assert("保存后文件存在", File.Exists(storePath));

    var loaded = store.Load();
    Assert("加载后 2 个列表", loaded.Count == 2);
    Assert("列表[0] Name=晨间", loaded[0].Name == "晨间");
    Assert("列表[0] 2 首", loaded[0].Tracks.Count == 2);
    Assert("列表[1] 1 首", loaded[1].Tracks.Count == 1);
}
finally
{
    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
}

Console.WriteLine("\n=== TaskStore Export/Import 测试 ===");
try
{
    var exportPath = Path.Combine(tempDir, "tasks.json");
    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    Directory.CreateDirectory(tempDir);

    var store = new TestTaskStore(Path.Combine(tempDir, "tasks.json"));
    var tasks = new List<PlayTask>
    {
        new() { Name = "T1", StartTime = new TimeSpan(8,0,0), EndTime = new TimeSpan(9,0,0), Repeat = new RepeatRule { Kind = RepeatKind.Daily }, Tracks = new() { new() { FilePath = "a.mp3" } } },
        new() { Name = "T2", StartTime = new TimeSpan(20,0,0), EndTime = new TimeSpan(21,0,0), Repeat = new RepeatRule { Kind = RepeatKind.Daily }, Tracks = new() { new() { FilePath = "b.mp3" } } }
    };
    store.Export(exportPath, tasks);
    Assert("导出文件存在", File.Exists(exportPath));

    var (imported, error) = store.Import(exportPath);
    Assert("导入无错误", error is null);
    Assert("导入 2 个任务", imported.Count == 2);
    Assert("导入任务被重新分配 Id", imported[0].Id != tasks[0].Id);
    Assert("导入任务名 T1 保留", imported[0].Name == "T1");
    Assert("导入任务时间保留", imported[1].StartTime == new TimeSpan(20, 0, 0));

    // 非法文件
    var (bad, err) = store.Import(Path.Combine(tempDir, "nope.json"));
    Assert("导入不存在文件返回错误", err is not null && bad.Count == 0);
}
finally
{
    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
}

Console.WriteLine($"\n结果：{passed} 通过 / {failed} 失败");
if (failed > 0) Environment.Exit(1);
Console.WriteLine("✓ 所有测试通过！");

// 测试用 PlayListStore 包装（指向临时路径）
public class TestPlayListStore : PlayListStore
{
    public TestPlayListStore(string path)
    {
        var t = typeof(PlayListStore);
        var f = t.GetProperty(nameof(FilePath));
        f!.SetValue(this, path);
    }
}

public class TestTaskStore : TaskStore
{
    public TestTaskStore(string path)
    {
        var t = typeof(TaskStore);
        var f = t.GetProperty(nameof(FilePath));
        f!.SetValue(this, path);
    }
}
