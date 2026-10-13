using System;
using System.Windows;
using MusicScheduler.Services;
using MusicScheduler.ViewModels;

namespace MusicScheduler.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var state = App.GetService<AppState>();
        DataContext = new SettingsViewModel(state);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
