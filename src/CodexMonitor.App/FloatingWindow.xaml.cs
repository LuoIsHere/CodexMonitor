using System.Windows;
using System.Windows.Input;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.App.Windows;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App;

public partial class FloatingWindow : Window
{
    private readonly FloatingWindowViewModel _viewModel;
    private bool _isLocked;
    private bool _initialPositionApplied;
    private bool _allowClose;

    public FloatingWindow(
        FloatingWindowViewModel viewModel,
        FloatingWindowSettings settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        if (settings.Left is double left)
        {
            Left = left;
        }

        if (settings.Top is double top)
        {
            Top = top;
        }
        _isLocked = settings.IsLocked;
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        Closing += OnClosing;
    }

    public event EventHandler<FloatingWindowPositionChangedEventArgs>? PositionChanged;

    public void ApplySettings(FloatingWindowSettings settings)
    {
        _viewModel.ApplyDisplaySettings(settings.Display);
        SetLocked(settings.IsLocked);
    }

    public void SetLocked(bool isLocked)
    {
        _isLocked = isLocked;
        Topmost = isLocked;
        WindowClickThroughService.SetClickThrough(this, isLocked);
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
        => SetLocked(_isLocked);

    private void OnContentRendered(object? sender, EventArgs e)
    {
        if (_initialPositionApplied)
        {
            return;
        }

        _initialPositionApplied = true;
        ApplyInitialPosition();
    }

    private void ApplyInitialPosition()
    {
        if (double.IsNaN(Left) || double.IsNaN(Top))
        {
            Left = SystemParameters.WorkArea.Right - ActualWidth - 16;
            Top = SystemParameters.WorkArea.Bottom - ActualHeight - 16;
        }

        EnsureVisibleOnVirtualScreen();
    }

    private void EnsureVisibleOnVirtualScreen()
    {
        var minimumLeft = SystemParameters.VirtualScreenLeft;
        var maximumLeft = SystemParameters.VirtualScreenLeft +
                          SystemParameters.VirtualScreenWidth - ActualWidth;
        var minimumTop = SystemParameters.VirtualScreenTop;
        var maximumTop = SystemParameters.VirtualScreenTop +
                         SystemParameters.VirtualScreenHeight - ActualHeight;

        Left = Math.Clamp(Left, minimumLeft, Math.Max(minimumLeft, maximumLeft));
        Top = Math.Clamp(Top, minimumTop, Math.Max(minimumTop, maximumTop));
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isLocked || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
            EnsureVisibleOnVirtualScreen();
            PositionChanged?.Invoke(
                this,
                new FloatingWindowPositionChangedEventArgs(Left, Top));
        }
        catch (InvalidOperationException)
        {
            // The mouse button can be released before WPF starts the drag loop.
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }
}

public sealed class FloatingWindowPositionChangedEventArgs(double left, double top) : EventArgs
{
    public double Left { get; } = left;

    public double Top { get; } = top;
}
