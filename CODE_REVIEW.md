# MusicScheduler 代码审查报告

> **2026-11-14 更新：P0 全部 8 项已修复并端到端验证通过**（EQ 跨曲目生效、编辑冲突回滚、损坏文件备份、Toast 宿主缓存与链路兜底、EQ 系数原子交换、手动播放接调度器、DbText 刷新），见第六节。
> 审查范围：全部 35 个源文件（Services / Models / ViewModels / Views / Styles / Converters / Tests / Installer）
> 审查方式：全文精读核心并发代码 + 交叉验证，所有问题均附文件与行号证据
> 日期：2026-11-14

---

## 总体评价

架构清晰（MVVM + 服务层），核心调度引擎、冲突检测、星期掩码位运算、任务 JSON 原子写入均实现正确，无安全漏洞。主要问题集中在：**并发细节、错误路径的数据一致性、少量资源泄漏、展示字段误持久化**。无致命架构缺陷。

| 类别 | 数量 |
|---|---|
| 高危 BUG（P0） | 8 |
| 中危问题（P1） | 13 |
| 冗余代码 | 9 |
| 设计问题 | 7 |

---

## 一、高危 BUG（P0）

### 1. 任务专属 EQ 预设只在第一首歌生效 ⚠️（回归自 EQ 快照功能）

- `Services\MusicPlaybackService.cs:389-418`：曲目自然播完进入 `OnPlaybackStoppedInternal` → `StopInternal(false)` → 第 457 行 `RestoreGlobalEqualizer()` 把全局 EQ **提前还原**了 → 随后 `PlayCurrent()`（第 326 行）用已还原的全局 EQ 构建信号链。
- 同样受影响的路径：`Next()`（288）、`Previous()`（299）、`PlayCurrent` 播放失败重试（347）。
- **后果**：任务设了"低音增强"，从第二首歌起音效变回全局平坦。
- **修法**：`RestoreGlobalEqualizer` 只应在任务级结束路径调用（`Stop()` 公开方法 / 调度器 `TaskEnded`），曲目级切换（`OnPlaybackStoppedInternal` 内部续播、Next/Previous）跳过还原。

### 2. 编辑任务"先写后验"，冲突拒绝后内存被污染

- `ViewModels\TaskEditViewModel.cs:250-262` 先把所有字段写回 `_editing`（它就是 `_state.Tasks` 里的同一实例），`264` 行才做冲突检测，`269` 行冲突时 `return false`。
- **后果**：保存失败但主界面卡片立即显示新时间、内存态与磁盘态永久不一致（用户点"取消"也无法回滚）。
- **修法**：先用局部值做冲突检测，通过后再写回；或写快照，失败时还原。

### 3. Toast 通知宿主泄漏（视觉树无限累积）

- `Services\ToastService.cs:42` `main.FindName("ToastHost")` 永远返回 `null`——全项目 XAML 无 `x:Name="ToastHost"`，而 52 行 `host.Name = "ToastHost"` 只赋属性、未调 `Window.RegisterName`，不会进入 NameScope。
- 于是每条 Toast 都走 45-59 行新建一个 host `StackPanel` 并 `children.Add(host)`；Toast 消失只移除 border，**host 永不移除**。用一天托盘通知几十次就积累几十个空 Panel。
- **修法**：单例服务用成员字段缓存 host（首次创建后复用）。

### 4. EQ 滤波器系数更新与音频线程竞态（锁形同虚设）

- `Services\EqualizerSampleProvider.cs:38-41` `Update()` 逐个改写 `BiQuadFilter` 的 a1~a5 系数；`Read()`（55-58 行）在 WASAPI 音频线程调用，不持任何锁。
- `MusicPlaybackService.cs:124-130` 虽用 `lock (_lock)` 包住 Update，但音频线程根本不走这把锁。
- **后果**：播放中拖 EQ 滑杆时，音频线程可能读到新旧系数混合状态，理论上产生瞬时爆音（64 位下 double 写入近似原子，概率低但真实存在）。
- **修法**：Update 构建新 `BiQuadFilter[]` 数组后原子交换引用（`Interlocked.Exchange` 或 `_bands = newArray`），Read 端每帧取局部引用。

### 5. tasks.json 损坏 → 静默清空 + 下次保存覆盖（任务数据永久丢失）

- `Services\TaskStore.cs:45-49`：反序列化失败仅记日志并返回空列表 → UI 显示"0 个任务" → 用户任何增删操作触发 `Save()`（52-66 行）直接把坏文件覆盖成空列表，**旧数据无法找回**。
- **修法**：Load 失败时先把坏文件改名为 `tasks.json.corrupt-<时间戳>`，同时 Toast 提醒用户。

### 6. 手动播放（▶ 立即）与调度器状态机冲突

- `ViewModels\MainViewModel.cs:113` `_playback.Start(task)` 绕过调度器，不更新 `_currentRunningTask/_currentEndAt`。
- `Services\TaskSchedulerService.cs:123-141`：若之前有定时任务在跑，其 `_currentEndAt` 仍在计时，到点 `_playback.Stop()` 会**把手动播放砍停**；反之手动播放也不享受淡入淡出。
- **修法**：RunNow 改走调度器（如 `scheduler.PlayManually(task)` 统一管理生命周期），或调度器监听 `Playback.ActiveTask` 变化。

### 7. EQ 频段 dB 标签永不刷新

- `Views\MainWindow.xaml.cs:291-296`：`EqBandVm.DbText` 依赖 `_value`，但 `Value` setter（294 行）只通知 `Value` 属性；XAML 绑定 `{Binding DbText}`，拖动滑块后 dB 文本停留初始值。
- **修法**：setter 中追加 `OnPropertyChanged(nameof(DbText))`。

### 8. Toast 动画链断裂导致通知永久失效

- `Services\ToastService.cs:97-101`：`slideOut.Completed` 是唯一解锁 `_showing` 的路径；若主窗口在动画完成前关闭/隐藏，`Completed` 不触发 → `_showing` 卡死为 true → **之后所有 Toast 永久积压**（队列无上限）。
- **修法**：`ShowNext()` 开头做防御性解锁（如基于时间戳超时）；或用 `DispatcherTimer` 兜底移除。

---

## 二、中危问题（P1）

| # | 问题 | 证据 | 说明 |
|---|---|---|---|
| 1 | `settings.json` 非原子写入 | `Services\AppState.cs:139` 直接 `File.WriteAllText` | TaskStore/PlayListStore 都是 tmp+Replace，唯独设置文件是裸写；崩溃可损坏 |
| 2 | LoadSettings 按键序赋 EQ 频段 | `AppState.cs:100-108`（`idx++` 只看出现顺序） | 手工编辑 JSON 改键序 → 增益错位到错误频段；应解析 `p.Name` 里的数字 |
| 3 | LoadSettings 期间回写注册表 | `AppState.cs:94-97` 赋 `AutoStart` → `OnAutoStartChanged` → `SystemService.SetAutoStart` | `_suppressSave` 只抑制保存不抑制注册表；安装器写入的自启项可能被旧 settings.json 值抹掉 |
| 4 | EQ 预设名三处硬编码 | `Views\MainWindow.xaml:228-235`（8 个 ComboBoxItem）、`Models\EqualizerSettings.cs:81`、`TaskEditViewModel.cs:42` | 任一处增删/改名 → SelectedIndex 错位 → 选"人声"应用成"电子" |
| 5 | 未知 EQ 预设映射成"流行" | `TaskEditViewModel.cs:96` `Math.Max(1, IndexOf+1)`：IndexOf=-1 时 Max(1,0)=1 | 旧数据/手改 JSON 的未知预设名落到第 1 项而非"跟随全局"；应 `IndexOf<0 ? 0 : IndexOf+1` |
| 6 | 复制任务"先落盘后弹窗" | `MainViewModel.cs:72-79`：先 Add+Save 再 ShowDialog，取消不回滚 | 取消编辑仍留下"副本"，与新建行为不一致（幽灵任务） |
| 7 | 保存失败被静默吞掉 | `TaskStore.cs:62-65`、`PlayListStore.cs:61-64`、`AppState.cs:127-128` | 磁盘满/文件被占用时 UI 照常提示"已保存" |
| 8 | 调度器后台线程直接改 UI 绑定属性 | `TaskSchedulerService.cs:167`（后台写 `task.HasRun`）、`MainViewModel.cs:39-45` | 依赖 WPF 隐式 marshal 才不崩；`MainWindow.xaml.cs:255` 用了显式 Dispatcher，风格不统一 |
| 9 | 进度条 Seek 位置有偏差 | `MainWindow.xaml.cs:50-52` 按控件宽算比例 | Slider 两端有 Thumb 半径留白，长曲目点击两端偏移可达数秒 |
| 10 | 展示字段/含 IO 的 getter 被序列化 | `PlayTask.cs:71/87/116`（无 [JsonIgnore]）、`TrackInfo.cs:18/20/22`、`PlayList.cs:16` | tasks.json 存了 RepeatDisplay/TimeRangeDisplay/StatusDisplay 死字段；`Exists` getter 每次保存对每首歌做 `File.Exists` IO |
| 11 | setup.iss 卸载清理路径无效 | `Installer\setup.iss:71-74` 清 `{app}\logs`、`{app}\tasks.json` | 真实数据在 `%APPDATA%\MusicScheduler\`，卸载残留（是否删用户数据另议，但目前写了等于没写） |
| 12 | build-release.ps1 不检查 publish 退出码 | `Installer\build-release.ps1:34-41/52-57/73-78` | publish 失败仍继续，ISCC 会把**旧 payload** 打进安装包；`$ErrorActionPreference=Stop` 对 native 命令无效 |
| 13 | Logger 无轮转/上限 | `Services\Logger.cs` 固定 `log.txt` + `File.AppendAllText` | 托盘常驻应用长期运行 log.txt 无限膨胀 |

### 数据校验缺口

- **跨午夜任务被静默跳过**：`ConflictDetector.cs:18` `EndTime <= StartTime` 直接返回（UI 有校验，但 `TaskStore.Import` 不校验）→ 导入的 23:50-00:10 任务**永不触发且无提示**（调度器 81 行同样返回 null）。
- **空规则可保存**：Weekly + 全不选、Once + 日期为 null 的任务可入库，永不触发。

---

## 三、冗余代码

| # | 内容 | 证据 | 处理建议 |
|---|---|---|---|
| 1 | 3 个转换器零引用 | `Converters.cs:26-54`（TimeSpanToString/PlaybackModeToText/TimeSpanToSeconds） | 删除 |
| 2 | 2 个转换器声明未绑定 | `App.xaml:22,24`、`MainWindow.xaml:16,18` 的 InverseBool/StringToVis | 删除 |
| 3 | 窗口级重复声明 App 级资源 | `MainWindow.xaml:15-19`、`TaskEditWindow.xaml:11-12` | 统一只留 App.xaml 一份 |
| 4 | 4 个按钮模板仅触发器值不同 | `Styles\Buttons.xaml:16-38/51-74/88-104/113-127` | 合并为 1 个基于附加属性的模板 |
| 5 | ComboBoxItem 前景样式三份 | `Styles\Texts.xaml:81-95`、`MainWindow.xaml:215-227`、`TaskEditWindow.xaml:170-174` | 合并 |
| 6 | AppliesOn + MatchesWeekly 逐字重复 | `ConflictDetector.cs:81-96` ≡ `TaskSchedulerService.cs:94-109` | 抽到 `RepeatRule.AppliesOn(day)` 方法（顺带给模型补 NextOccurrence API） |
| 7 | TaskStore ≈ PlayListStore 80% 重复 | 两文件的 Options/DataDir/原子写 Load/Save | 抽 `JsonStore<T>` 基类（顺带统一修复原子性与损坏备份） |
| 8 | 死代码若干 | `PlayListStore.cs:68-72` FindById/FindByName 零调用；`AppState.cs:22` ScheduleHorizonDays 从未接线（冲突检测硬编码 7）；`TrackInfo.cs:22` DisplayDuration 无引用；`setup.iss:82-89` ShouldAddAutoStartTask 未使用；`PlayTask.cs:14-16` `[NotifyPropertyChangedFor(nameof(Name))]` 是复制粘贴失误 | 删除或接线 |
| 9 | 卡片悬浮动画用 code-behind | `MainWindow.xaml.cs:122-138` + `Animations.xaml:25-39` 两个只差一个 To 值的 Storyboard | 改 `Style.Triggers` + EnterActions/ExitActions，删 code-behind |

---

## 四、设计问题（不过度重构，仅指出）

1. **自造 ServiceLocator 与 DI 并存**：`App.xaml.cs:14,27-31` 手工 `Dictionary<Type,object>` + `GetService<T>`，`TaskEditWindow.xaml.cs:16-19` 四连取；而 `MainViewModel` 又是构造注入。二选一即可，就本项目规模甚至静态单例字段更简单。
2. **播放引擎兼任 UI ViewModel**：`MusicPlaybackService` 同时持有 NAudio 链路和 `IsPlaying/CurrentTitle/CurrentPositionSeconds` 等 UI 状态，后台监控线程直接赋值。当前能跑，但引擎与表现层耦合，进度状态宜收敛为独立 VM。
3. **EQ 的 VM 逻辑写在 code-behind**：`MainWindow.xaml.cs:140-200` SetupEqualizer + 285-297 `EqBandVm` 尾随类，属 `EqualizerViewModel` 职责。
4. **VM 依赖 Window**：`TaskEditViewModel.SaveAndClose(Window)`（253 行）反向耦合 View，应用回调/事件替代；对话框 `ShowDialog()` 无 Owner（`MainViewModel.cs:56-65/123/129`），主窗口在托盘时弹窗会游离。
5. **控件状态当数据源**：`TaskEditWindow.xaml:61,67` 绑定 `RepeatKindCombo.SelectedIndex` 而非 VM 的 `RepeatKindIndex`，与枚举顺序隐式耦合。
6. **版本号四处硬编码**：`setup.iss:6`、`build-release.ps1:21`、`AssemblyInfo.cs:10-11`、dist 文件名，发布需人工同步四处。
7. **双 500ms 定时器**：`MainViewModel.cs:47` 的时钟与 `TaskSchedulerService.cs:38` 的调度 timer 各一份，`GetNextPendingTask` 每 tick 对每个任务做 8 天循环扫描，可缓存上次计算结果（无功能问题，纯优化空间）。

---

## 五、做得好的地方（无需改动）

- `TaskStore.Save` tmp + `File.Replace` 原子写、单实例保护、HKCU 自启（无需管理员）
- 冲突检测区间重叠算法正确（含边界相接不冲突）；星期掩码 `1 << (int)dow` 与 Sunday=1<<0 定义一致
- 播放失败自动跳过坏文件、EQ 快照设计意图正确（只是还原时机错了）
- 测试与当前模型字段同步，未见改名失效

---

## 六、修复优先级建议

| 优先级 | 事项 | 预估 |
|---|---|---|
| P0-立即 | #1 任务 EQ 只在第一首生效、#2 编辑先写后验、#5 tasks.json 损坏备份、#7 DbText | 各 10-30 分钟 |
| P0-尽快 | #3 Toast 宿主泄漏、#8 Toast 链断裂、#4 EQ 系数原子交换、#6 手动播放/调度器冲突 | 各 30-60 分钟 |
| P1 | 展示字段加 [JsonIgnore]、settings.json 原子写、保存失败上抛、EQ 预设单一数据源、Import 校验跨午夜 | 半天 |
| 冗余清理 | 死转换器/死方法/重复样式/JsonStore<T> 抽取 | 半天~1 天 |

**不建议**做的重构：引入 Microsoft.Extensions.DependencyInjection、把 MusicPlaybackService 拆引擎+VM、Styles 大规模合并——项目规模下收益低、回归风险高，列为"知道即可"。
