using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using H.NotifyIcon;
using MusicScheduler.Models;
using MusicScheduler.Services;
using MusicScheduler.ViewModels;

namespace MusicScheduler.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _reallyClose;
    private readonly List<EqBandVm> _eqBands = new();
    private bool _suppressEqPresetUpdate;

    // ---------- 进度条拖拽快进/后退 ----------

    private void ProgressSlider_DragStart(object sender, MouseButtonEventArgs e)
    {
        // 鼠标按下即冻结后台位置回推，防止拖动/点击被 OneWay 绑定覆盖
        if (sender is Slider s && s.IsEnabled)
        {
            _vm.Playback.IsSeeking = true;
        }
    }

    private void ProgressSlider_DragDelta(object sender, DragDeltaEventArgs e)
    {
        // 拖动过程中仅刷新时间显示，松手后才真正跳转
        if (sender is Slider s)
        {
            _vm.Playback.UpdatePositionDisplay(s.Value);
        }
    }

    private void ProgressSlider_DragEnd(object sender, MouseButtonEventArgs e)
    {
        if (sender is Slider s && s.IsEnabled)
        {
            _vm.Playback.IsSeeking = false;
            // 直接按松开位置在轨道上的比例计算目标，兼容点击与拖拽两种方式
            var pos = e.GetPosition(s);
            var ratio = s.ActualWidth > 0 ? Math.Clamp(pos.X / s.ActualWidth, 0, 1) : 0;
            _vm.Playback.Seek(ratio * s.Maximum);
        }
    }

    private void ProgressSlider_LostCapture(object sender, MouseEventArgs e)
    {
        // 兜底：鼠标捕获丢失时解除冻结，避免进度显示永久停住
        _vm.Playback.IsSeeking = false;
    }

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        try
        {
            var sri = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            if (sri?.Stream is not null)
            {
                using var icon = new Icon(sri.Stream);
                TrayIcon.Icon = (Icon)icon.Clone();
            }
        }
        catch { }

        SetupTrayMenu();
        SetupEqualizer();
    }

    private bool _animationsActive;

    private void UpdatePlayAnimations(bool isPlaying)
    {
        // 动画只是视觉锦上添花，任何异常都不得影响播放流程
        try
        {
            var breathe = (System.Windows.Media.Animation.Storyboard)FindResource("CoverBreathe");
            var pulse = (System.Windows.Media.Animation.Storyboard)FindResource("StatusDotPulse");

            if (isPlaying)
            {
                StatusDot.Visibility = Visibility.Visible;
                StatusDot.Fill = (System.Windows.Media.Brush)FindResource("Success");
                if (!_animationsActive)
                {
                    // isControllable: true 才允许后续 Stop 交互控制
                    Cover.BeginStoryboard(breathe, System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace, true);
                    StatusDot.BeginStoryboard(pulse, System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace, true);
                    _animationsActive = true;
                }
            }
            else
            {
                if (_animationsActive)
                {
                    try { breathe.Stop(Cover); } catch { }
                    try { pulse.Stop(StatusDot); } catch { }
                    _animationsActive = false;
                }
                StatusDot.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            // 忽略动画层异常，播放不受影响
        }
    }

    private void TaskCard_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Border b)
        {
            var story = (System.Windows.Media.Animation.Storyboard)FindResource("CardHoverIn");
            b.BeginStoryboard(story);
        }
    }

    private void TaskCard_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Border b)
        {
            var story = (System.Windows.Media.Animation.Storyboard)FindResource("CardHoverOut");
            b.BeginStoryboard(story);
        }
    }

    private void SetupEqualizer()
    {
        var eq = _vm.Equalizer;
        for (int i = 0; i < EqualizerSettings.BandCount; i++)
        {
            var idx = i;
            var band = new EqBandVm
            {
                Label = FormatFreqLabel(EqualizerSettings.CenterFrequencies[i]),
                Value = eq.GetBand(i)
            };
            band.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(EqBandVm.Value))
                {
                    eq.SetBand(idx, (float)band.Value);
                }
            };
            _eqBands.Add(band);
        }
        EqBands.ItemsSource = _eqBands;

        // 初始选中预设
        _suppressEqPresetUpdate = true;
        EqPresetCombo.SelectedIndex = Array.IndexOf(EqualizerSettings.AvailablePresets, eq.Preset);
        if (EqPresetCombo.SelectedIndex < 0) EqPresetCombo.SelectedIndex = 0;
        _suppressEqPresetUpdate = false;

        // 监听均衡器任意属性变更以同步预设显示与 10 段滑块
        eq.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EqualizerSettings.Preset) && !_suppressEqPresetUpdate)
            {
                Dispatcher.Invoke(() =>
                {
                    _suppressEqPresetUpdate = true;
                    var idx = Array.IndexOf(EqualizerSettings.AvailablePresets, eq.Preset);
                    if (idx >= 0) EqPresetCombo.SelectedIndex = idx;
                    _suppressEqPresetUpdate = false;
                });
                return;
            }
            // B0..B9 任一频段变化（任务启动应用预设时）→ 同步 10 个滑块
            if (e.PropertyName is not null
                && e.PropertyName.Length == 2
                && e.PropertyName[0] == 'B'
                && e.PropertyName[1] >= '0' && e.PropertyName[1] <= '9')
            {
                Dispatcher.Invoke(() =>
                {
                    for (int i = 0; i < _eqBands.Count; i++)
                    {
                        var newVal = (double)eq.GetBand(i);
                        // 仅在有差异时赋值（避免与 EqBandVm.Value → eq.SetBand 形成回环）
                        if (Math.Abs(_eqBands[i].Value - newVal) > 0.001)
                            _eqBands[i].Value = newVal;
                    }
                });
            }
        };
    }

    private static string FormatFreqLabel(float hz) => hz switch
    {
        >= 1000 => $"{hz / 1000:0}k",
        _ => $"{hz:0}"
    };

    private void EqPreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEqPresetUpdate) return;
        if (_vm?.Equalizer is null) return;  // 防止 InitializeComponent 期间 SelectedIndex 触发回调时 _vm 还未赋值
        if (EqPresetCombo.SelectedItem is ComboBoxItem item && item.Content is string name)
        {
            _vm.Equalizer.ApplyPreset(name);
            for (int i = 0; i < _eqBands.Count; i++)
            {
                _eqBands[i].Value = _vm.Equalizer.GetBand(i);
            }
        }
    }

    private void SetupTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var openItem = new System.Windows.Controls.MenuItem { Header = "打开主界面" };
        openItem.Click += (_, _) => ShowWindow();
        menu.Items.Add(openItem);
        var stopItem = new System.Windows.Controls.MenuItem { Header = "停止播放" };
        stopItem.Click += (_, _) => _vm.StopPlaybackCommand.Execute(null);
        menu.Items.Add(stopItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        var aboutItem = new System.Windows.Controls.MenuItem { Header = "关于" };
        aboutItem.Click += (_, _) => new AboutWindow().ShowDialog();
        menu.Items.Add(aboutItem);
        var exitItem = new System.Windows.Controls.MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => { _reallyClose = true; Application.Current.Shutdown(); };
        menu.Items.Add(exitItem);
        TrayIcon.ContextMenu = menu;
        TrayIcon.TrayLeftMouseUp += (_, _) => ShowWindow();
    }

    public void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true; Topmost = false;
        Focus();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TrayIcon.Visibility = Visibility.Visible;
        // 播放状态变化时联动动画（封面呼吸 + 状态点脉冲）
        _vm.Playback.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MusicPlaybackService.IsPlaying))
            {
                Dispatcher.Invoke(() => UpdatePlayAnimations(_vm.Playback.IsPlaying));
            }
        };
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
            try
            {
                TrayIcon?.ShowNotification(
                    title: "音乐定时管家",
                    message: "已最小化到系统托盘，定时任务继续在后台运行。",
                    icon: H.NotifyIcon.Core.NotificationIcon.Info);
            }
            catch
            {
                // 托盘通知失败不影响主流程
            }
        }
    }
}

/// <summary>
/// 单个均衡器频段 VM（双向绑定用）。</summary>
public class EqBandVm : ObservableObject
{
    private double _value;
    public string Label { get; set; } = string.Empty;
    public double Value
    {
        get => _value;
        set => SetProperty(ref _value, Math.Clamp(value, -12, 12));
    }
    public string DbText => $"{_value:+0;-0;0} dB";
}
