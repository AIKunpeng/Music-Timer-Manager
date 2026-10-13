using System;
using System.Windows;
using MusicScheduler.Models;
using MusicScheduler.Services;
using MusicScheduler.ViewModels;

namespace MusicScheduler.Views;

public partial class TaskEditWindow : Window
{
    private readonly TaskEditViewModel _vm;

    public TaskEditWindow(PlayTask? existing)
    {
        InitializeComponent();
        var state = App.GetService<AppState>();
        var store = App.GetService<TaskStore>();
        var scheduler = App.GetService<TaskSchedulerService>();
        var playlistStore = App.GetService<PlayListStore>();

        _vm = new TaskEditViewModel(existing, state, store, scheduler, playlistStore);
        DataContext = _vm;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => _vm.SaveAndClose(this);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
