using System.Windows;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.Core.Refresh;
using LuoIsHere.CodexMonitor.Infrastructure.Codex;
using LuoIsHere.CodexMonitor.Infrastructure.Logging;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

namespace LuoIsHere.CodexMonitor.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var logger = new FileAppLogger();
            var settings = await new JsonSettingsStore(logger).LoadAsync();
            var provider = new CodexAppServerQuotaProvider(
                new CodexExecutableLocator(),
                settings.CodexExecutable,
                logger);
            var refreshService = new QuotaRefreshService(provider, logger);
            var viewModel = new MainWindowViewModel(
                refreshService,
                TimeSpan.FromMinutes(settings.RefreshIntervalMinutes));

            var window = new MainWindow(viewModel);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"CodexMonitor 启动失败。\n\n{exception.Message}",
                "CodexMonitor",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}

