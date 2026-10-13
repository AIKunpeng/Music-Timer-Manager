using CommunityToolkit.Mvvm.ComponentModel;
using MusicScheduler.Services;

namespace MusicScheduler.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppState _state;

    public bool AutoStart
    {
        get => _state.AutoStart;
        set
        {
            if (_state.AutoStart == value) return;
            _state.AutoStart = value;
            OnPropertyChanged();
        }
    }

    public double DefaultVolume
    {
        get => _state.DefaultVolume;
        set
        {
            if (_state.DefaultVolume == value) return;
            _state.DefaultVolume = value;
            OnPropertyChanged();
        }
    }

    public double FadeSeconds
    {
        get => _state.FadeSeconds;
        set
        {
            if (_state.FadeSeconds == value) return;
            _state.FadeSeconds = value;
            OnPropertyChanged();
        }
    }

    public SettingsViewModel(AppState state)
    {
        _state = state;
    }
}
