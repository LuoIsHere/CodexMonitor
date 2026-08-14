using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App;

public sealed class FloatingWindowService : IFloatingWindowService
{
    private readonly MainWindowViewModel _sourceViewModel;
    private FloatingWindowSettings _settings;
    private FloatingWindowViewModel? _viewModel;
    private FloatingWindow? _window;
    private bool _disposed;

    public FloatingWindowService(
        MainWindowViewModel sourceViewModel,
        FloatingWindowSettings settings)
    {
        _sourceViewModel = sourceViewModel;
        _settings = settings;

        if (settings.Enabled)
        {
            ShowWindow();
        }
    }

    public bool IsAvailable => true;

    public bool IsEnabled => _settings.Enabled;

    public bool IsLocked => _settings.IsLocked;

    public FloatingWindowSettings CurrentSettings => _settings;

    public event EventHandler? StateChanged;

    public event EventHandler<FloatingWindowSettingsChangedEventArgs>? SettingsChanged;

    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_settings.Enabled == enabled)
        {
            return;
        }

        _settings = _settings with { Enabled = enabled };
        if (enabled)
        {
            ShowWindow();
        }
        else
        {
            _window?.Hide();
        }

        NotifySettingsChanged();
    }

    public void SetLocked(bool isLocked)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_settings.IsLocked == isLocked)
        {
            return;
        }

        _settings = _settings with { IsLocked = isLocked };
        _window?.SetLocked(isLocked);
        NotifySettingsChanged();
    }

    public void ApplySettings(FloatingWindowSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _settings = settings;

        if (settings.Enabled)
        {
            ShowWindow();
            _window?.ApplySettings(settings);
        }
        else
        {
            _window?.ApplySettings(settings);
            _window?.Hide();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowWindow()
    {
        EnsureWindow();
        _window!.Show();
        _window.SetLocked(_settings.IsLocked);
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _viewModel = new FloatingWindowViewModel(_sourceViewModel, _settings.Display);
        _window = new FloatingWindow(_viewModel, _settings);
        _window.PositionChanged += OnWindowPositionChanged;
    }

    private void OnWindowPositionChanged(object? sender, FloatingWindowPositionChangedEventArgs e)
    {
        _settings = _settings with
        {
            Left = e.Left,
            Top = e.Top,
        };
        SettingsChanged?.Invoke(this, new FloatingWindowSettingsChangedEventArgs(_settings));
    }

    private void NotifySettingsChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
        SettingsChanged?.Invoke(this, new FloatingWindowSettingsChangedEventArgs(_settings));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_window is not null)
        {
            _window.PositionChanged -= OnWindowPositionChanged;
            _window.CloseForExit();
            _window = null;
        }

        _viewModel?.Dispose();
        _viewModel = null;
    }
}
