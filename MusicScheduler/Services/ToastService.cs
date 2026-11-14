using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MusicScheduler.Services;

/// <summary>
/// 右下角 Toast 通知服务。单例，全局可调用。
/// 使用方式：ToastService.Instance.Show("消息", "success")。
/// </summary>
public class ToastService
{
    public static ToastService Instance { get; } = new();
    private ToastService() { }

    private readonly ConcurrentQueue<ToastItem> _queue = new();
    private bool _showing;
    private readonly Dispatcher _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    /// <summary>缓存宿主容器：Window.FindName 无法命中代码创建且未 RegisterName 的元素，每次 FindName 都返回 null 会导致宿主无限累积。</summary>
    private Panel? _toastHost;

    public void Show(string message, string level = "info", double durationSeconds = 3)
    {
        _dispatcher.BeginInvoke(() =>
        {
            _queue.Enqueue(new ToastItem(message, level, durationSeconds));
            if (!_showing) ShowNext();
        });
    }

    private void ShowNext()
    {
        if (!_queue.TryDequeue(out var item)) { _showing = false; return; }
        _showing = true;

        var main = Application.Current?.MainWindow;
        if (main is null) { _showing = false; return; }

        // 创建容器（单例服务用字段缓存，避免每次 Toast 新建宿主）
        var host = _toastHost;
        if (host is null)
        {
            if (main.Content is not Grid g) { _showing = false; return; }
            host = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 24, 56),
                IsHitTestVisible = false
            };
            Grid.SetRowSpan(host, Math.Max(1, g.RowDefinitions.Count));
            Grid.SetColumnSpan(host, Math.Max(1, g.ColumnDefinitions.Count));
            g.Children.Add(host);
            _toastHost = host;
        }

        var border = CreateToastBorder(item);
        host.Children.Add(border);

        // 滑入动画
        var slideIn = new ThicknessAnimation
        {
            From = new Thickness(220, 0, 0, 0),
            To = new Thickness(0, 0, 0, 0),
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var opacityIn = new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = TimeSpan.FromMilliseconds(220)
        };
        border.BeginAnimation(Border.MarginProperty, slideIn);
        border.BeginAnimation(UIElement.OpacityProperty, opacityIn);

        // 自动消失
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(item.DurationSeconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var slideOut = new ThicknessAnimation
            {
                To = new Thickness(220, 0, 0, 0),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220)
            };
            border.BeginAnimation(Border.MarginProperty, slideOut);
            border.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            // 兜底移除：不依赖动画 Completed（窗口关闭/隐藏时 Completed 不触发，
            // 会卡死 _showing 导致后续 Toast 永久积压）。280ms > 220ms 动画时长。
            var cleanup = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            cleanup.Tick += (_, _) =>
            {
                cleanup.Stop();
                host.Children.Remove(border);
                ShowNext();
            };
            cleanup.Start();
        };
        timer.Start();
    }

    private static Border CreateToastBorder(ToastItem item)
    {
        var brush = item.Level switch
        {
            "success" => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
            "danger" => new SolidColorBrush(Color.FromRgb(0xFF, 0x52, 0x52)),
            "warning" => new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x00)),
            _ => new SolidColorBrush(Color.FromRgb(0x6C, 0x5C, 0xE7))
        };
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x34, 0x34, 0x4A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x7A)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 18, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Opacity = 0.4,
                BlurRadius = 16,
                ShadowDepth = 0
            },
            Opacity = 0,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new Border
                    {
                        Width = 3,
                        Background = brush,
                        Margin = new Thickness(-14, -10, 10, -10),
                        CornerRadius = new CornerRadius(2, 0, 0, 2)
                    },
                    new TextBlock
                    {
                        Text = item.Message,
                        Foreground = Brushes.White,
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center,
                        MaxWidth = 360,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
        return border;
    }

    private record ToastItem(string Message, string Level, double DurationSeconds);
}
