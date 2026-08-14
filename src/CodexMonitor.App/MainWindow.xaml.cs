using System.ComponentModel;
using System.Windows;
using System.Windows.Shell;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.App.Windows;

namespace LuoIsHere.CodexMonitor.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        ConfigureBackdropMode();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        ContentRendered += OnContentRendered;
        Closing += OnClosing;
    }

    public event EventHandler? HiddenToTray;

    private void ConfigureBackdropMode()
    {
        if (WindowBackdropService.RequiresLayeredTransparencyFallback())
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            if (WindowChrome.GetWindowChrome(this) is { } chrome)
            {
                chrome.GlassFrameThickness = new Thickness(0);
            }
            WindowSurface.CornerRadius = new CornerRadius(14);
            TitleBarSurface.CornerRadius = new CornerRadius(14, 14, 0, 0);
            return;
        }

        WindowSurface.Background = System.Windows.Media.Brushes.Transparent;
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        WindowBackdropService.ApplyDarkAcrylic(this);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.StartAsync();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        HiddenToTray?.Invoke(this, EventArgs.Empty);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();

    public void ShowFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    public void MinimizeFromTray()
    {
        if (IsVisible)
        {
            WindowState = WindowState.Minimized;
        }
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }
}
