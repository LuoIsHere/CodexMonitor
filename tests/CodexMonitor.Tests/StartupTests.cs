using System.IO;
using LuoIsHere.CodexMonitor.Core.Localization;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ellipse = System.Windows.Shapes.Ellipse;
using System.Xml.Linq;
using LuoIsHere.CodexMonitor.App;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.App.Monitoring;
using LuoIsHere.CodexMonitor.Core.Abstractions;
using LuoIsHere.CodexMonitor.Core.Models;
using LuoIsHere.CodexMonitor.Core.Refresh;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;
using LuoIsHere.CodexMonitor.Infrastructure.Startup;

internal static class StartupTests
{
    private const string Executable = @"C:\工具 with spaces\CodexMonitor.exe";
    private const string Command = "\"" + Executable + "\" --autostart";

    public static (string Name, Action Run)[] Cases =>
    [
        ("startup arguments and visibility", TestStartupOptions),
        ("secondary instance activation policy", () => TestSecondaryInstanceAsync().GetAwaiter().GetResult()),
        ("new and legacy startup defaults without registration", TestMigration),
        ("startup quoting and idempotent registration", TestRegistration),
        ("invalid startup targets rejected", TestInvalidTargets),
        ("read-only startup status", TestStatus),
        ("unrelated saves preserve missing or system-disabled entry", () => TestUnrelatedSavesAsync().GetAwaiter().GetResult()),
        ("cancelled or reverted startup edit has no effect", () => TestCancelledEditAsync().GetAwaiter().GetResult()),
        ("explicit startup enable disable and relocation", () => TestExplicitChangesAsync().GetAwaiter().GetResult()),
        ("registration failure does not save preference", () => TestRegistrationFailureAsync().GetAwaiter().GetResult()),
        ("partial settings failure retries without registry replay", () => TestPartialFailureAsync().GetAwaiter().GetResult()),
        ("background WPF refresh lifecycle and shared floating state", TestWpfLifecycle),
    ];

    private static UserStartupService Service(MemoryStartupStore store, string? path = Executable,
        bool exists = true, string assembly = "CodexMonitor")
        => new(store, new StartupExecutableResolver(() => path, () => assembly, _ => exists));

    private static void TestStartupOptions()
    {
        var defaults = new StartupSettings();
        Check(!defaults.Enabled && defaults.MinimizeToTray, "startup defaults");
        Check(StartupOptions.Parse([]).ShouldShowMainWindow(defaults), "manual launch shows main window");
        foreach (var argument in new[] { "--autostart", "-autostart", "--AUTOSTART" })
        {
            var options = StartupOptions.Parse([argument]);
            Check(options.IsAutomatic && !options.ShouldShowMainWindow(defaults), "automatic launch hides main window");
            Check(options.ShouldShowMainWindow(defaults with { MinimizeToTray = false }), "explicit visible automatic launch");
        }
        Check(!StartupOptions.Parse(["--autostart-other"]).IsAutomatic, "exact argument matching");
    }

    private static async Task TestSecondaryInstanceAsync()
    {
        var activations = 0;
        Task<bool> Activate() { activations++; return Task.FromResult(true); }
        await StartupOptions.Parse(["--autostart"]).HandleSecondaryInstanceAsync(Activate);
        await StartupOptions.Parse(["-autostart"]).HandleSecondaryInstanceAsync(Activate);
        Check(activations == 0, "automatic second instance must not request activation");
        await StartupOptions.Parse([]).HandleSecondaryInstanceAsync(Activate);
        Check(activations == 1, "manual second instance requests activation once");
    }

    private static void TestMigration()
    {
        var previous = Environment.GetEnvironmentVariable("CODEX_MONITOR_HOME");
        var root = Path.Combine(Path.GetTempPath(), "CodexMonitor.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CODEX_MONITOR_HOME", root);
            var store = new JsonSettingsStore(new SilentLogger());
            var registry = new MemoryStartupStore();
            var settings = store.LoadAsync().GetAwaiter().GetResult();
            Check(settings.SchemaVersion == 4 && !settings.Startup.Enabled, "first run defaults");
            for (var schema = 1; schema <= 3; schema++)
            {
                File.WriteAllText(AppPaths.SettingsFile, $$"""
                    { "schemaVersion": {{schema}}, "refreshIntervalMinutes": 9,
                      "notifications": { "enabled": false },
                      "floatingWindow": { "enabled": true, "left": 123, "top": 456 },
                      "startup": { "enabled": true, "minimizeToTray": false } }
                    """);
                settings = store.LoadAsync().GetAwaiter().GetResult();
                Check(settings.SchemaVersion == 4 && !settings.Startup.Enabled && settings.Startup.MinimizeToTray,
                    "old schema must never opt into startup");
                Check(settings.RefreshIntervalMinutes == 9 && !settings.Notifications.Enabled &&
                    settings.FloatingWindow.Enabled && settings.FloatingWindow.Left == 123, "legacy preferences preserved");
                var session = new StartupSettingsSession(settings, Service(registry), s => store.SaveAsync(s));
                var result = session.SaveAsync(settings, false).GetAwaiter().GetResult();
                Check(result.Success && registry.Writes == 0 && registry.Deletes == 0, "migration save has no registration");
            }
            File.WriteAllText(AppPaths.SettingsFile, "{\"schemaVersion\":4,\"startup\":null}");
            Check(!store.LoadAsync().GetAwaiter().GetResult().Startup.Enabled, "null startup section defaults");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_MONITOR_HOME", previous);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void TestRegistration()
    {
        var store = new MemoryStartupStore();
        var service = Service(store);
        service.Apply(true);
        Check(store.Value == Command && store.Writes == 1, "quoted Unicode path and argument");
        service.Apply(true);
        Check(store.Writes == 1, "same command is not rewritten");
        service.Apply(false);
        service.Apply(false);
        Check(store.Value is null && store.Deletes == 1, "delete only existing owned value");
    }

    private static void TestInvalidTargets()
    {
        foreach (var path in new string?[] { null, "", "CodexMonitor.exe", @"C:\runtime\dotnet.exe",
                     @"C:\app\CodexMonitor.dll", "C:\\bad\"path\\CodexMonitor.exe",
                     "C:\\" + new string('a', 250) + "\\CodexMonitor.exe" })
        {
            var store = new MemoryStartupStore();
            Throws(() => Service(store, path).Apply(true));
            Check(store.Writes == 0, "invalid executable must not register");
        }
        var missing = new MemoryStartupStore();
        Throws(() => Service(missing, exists: false).Apply(true));
        Throws(() => Service(missing, assembly: "OtherHost").Apply(true));
        Check(missing.Writes == 0, "missing file or non-application host rejected");
        var standalone = new MemoryStartupStore();
        Service(standalone, @"D:\便携目录\CodexMonitor-0.3.1-win-x64-self-contained.exe").Apply(true);
        Check(standalone.Value!.StartsWith("\"D:\\便携目录\\", StringComparison.Ordinal), "single-file process path");
    }

    private static void TestStatus()
    {
        var store = new MemoryStartupStore();
        var service = Service(store);
        Check(service.ReadStatus().Contains("未登记", StringComparison.Ordinal), "missing entry status");
        store.Value = Command;
        Check(service.ReadStatus().Contains("已登记", StringComparison.Ordinal), "registered status");
        store.Value = "\"C:\\old\\CodexMonitor.exe\" --autostart";
        Check(service.ReadStatus().Contains("不一致", StringComparison.Ordinal), "moved executable status");
        store.FailReads = true;
        Check(service.ReadStatus().Contains("无法确认", StringComparison.Ordinal), "read failure status");
        Check(store.Writes == 0 && store.Deletes == 0, "status never repairs registry");
    }

    private static async Task TestUnrelatedSavesAsync()
    {
        foreach (var entry in new string?[] { null, Command, "\"C:\\old\\CodexMonitor.exe\" --autostart" })
        {
            var registry = new MemoryStartupStore { Value = entry, SystemAllowsStartup = false };
            var settings = new AppSettings { Startup = new StartupSettings { Enabled = true } };
            for (var restart = 0; restart < 2; restart++)
            {
                var service = Service(registry);
                _ = service.ReadStatus();
                var session = new StartupSettingsSession(settings, service, _ => Task.CompletedTask);
                settings = settings with { Language = restart == 0 ? "en" : "zh-HK", RefreshIntervalMinutes = 13,
                    Startup = settings.Startup with { MinimizeToTray = false } };
                Check((await session.SaveAsync(settings, false)).Success, "unrelated settings saved");
            }
            Check(registry.Writes == 0 && registry.Deletes == 0 && !registry.SystemAllowsStartup && registry.Value == entry,
                "missing, stale and system-disabled entries remain untouched");
        }
    }

    private static async Task TestCancelledEditAsync()
    {
        var registry = new MemoryStartupStore();
        var viewModel = new SettingsWindowViewModel(new AppSettings());
        var session = new StartupSettingsSession(new AppSettings(), Service(registry), _ => Task.CompletedTask);
        viewModel.StartupEnabled = true;
        viewModel.RegisterCurrentPath = true;
        _ = viewModel.CreateSettings(); // Editing, including pending relocation, never commits.
        Check(registry.Writes == 0 && registry.Deletes == 0, "cancel without save is inert");
        viewModel.StartupEnabled = false;
        Check(!viewModel.RegisterCurrentPath, "disabling clears pending relocation");
        Check((await session.SaveAsync(viewModel.CreateSettings(), viewModel.RegisterCurrentPath)).Success, "reverted edit saves");
        Check(registry.Writes == 0 && registry.Deletes == 0, "reverted edit does not register");
    }

    private static async Task TestExplicitChangesAsync()
    {
        var registry = new MemoryStartupStore { SystemAllowsStartup = false };
        var settings = new AppSettings();
        var session = new StartupSettingsSession(settings, Service(registry), _ => Task.CompletedTask);
        settings = settings with { Startup = settings.Startup with { Enabled = true } };
        Check((await session.SaveAsync(settings, false)).Success && registry.Writes == 1, "explicit enable");
        registry.Value = "\"C:\\old\\CodexMonitor.exe\" --autostart";
        Check((await session.SaveAsync(settings, true)).Success && registry.Writes == 2, "explicit relocation");
        Check(!registry.SystemAllowsStartup, "system approval is never changed");
        settings = settings with { Startup = settings.Startup with { Enabled = false } };
        Check((await session.SaveAsync(settings, false)).Success && registry.Deletes == 1, "explicit disable");
    }

    private static async Task TestRegistrationFailureAsync()
    {
        foreach (var initialEnabled in new[] { false, true })
        {
            var registry = new MemoryStartupStore { Value = initialEnabled ? Command : null, FailWrites = true };
            var settings = new AppSettings { Startup = new StartupSettings { Enabled = initialEnabled } };
            var saves = 0;
            var session = new StartupSettingsSession(settings, Service(registry), _ => { saves++; return Task.CompletedTask; });
            var result = await session.SaveAsync(settings with { Startup = settings.Startup with { Enabled = !initialEnabled } }, false);
            Check(!result.Success && !result.StartupOperationCompleted && saves == 0, "registry error prevents preference commit");
            Check(registry.Value == (initialEnabled ? Command : null), "failed operation leaves entry intact");
        }
    }

    private static async Task TestPartialFailureAsync()
    {
        foreach (var initialEnabled in new[] { false, true })
        {
            var registry = new MemoryStartupStore { Value = initialEnabled ? Command : null };
            var settings = new AppSettings { Startup = new StartupSettings { Enabled = initialEnabled } };
            var saves = 0;
            var session = new StartupSettingsSession(settings, Service(registry), _ =>
            {
                saves++;
                return saves <= 2 ? Task.FromException(new IOException("simulated disk error")) : Task.CompletedTask;
            });
            settings = settings with { Startup = settings.Startup with { Enabled = !initialEnabled } };
            var first = await session.SaveAsync(settings, false);
            Check(!first.Success && first.StartupOperationCompleted && first.Error!.Contains("已完成", StringComparison.Ordinal),
                "partial success clearly reported");
            var operations = registry.Writes + registry.Deletes;
            // Simulate an external removal after the first attempt: retry must not repair it.
            registry.Value = null;
            var second = await session.SaveAsync(settings, false);
            Check(!second.Success && second.Error!.Contains("已完成", StringComparison.Ordinal), "partial result remains visible on repeated failure");
            var retry = await session.SaveAsync(settings, false);
            Check(retry.Success && !retry.StartupOperationCompleted && registry.Writes + registry.Deletes == operations,
                "configuration retry does not replay registry operation");
        }
    }

    private static void TestWpfLifecycle()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Dispatcher.InvokeAsync(async () =>
            {
                try { await TestWpfLifecycleAsync(); }
                catch (Exception exception) { failure = exception; }
                finally { app.Shutdown(); }
            });
            app.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Check(thread.Join(TimeSpan.FromSeconds(85)), "WPF test timeout");
        if (failure is not null) throw new InvalidOperationException($"WPF lifecycle failed: {failure.Message}", failure);
    }

    private static async Task TestWpfLifecycleAsync()
    {
        await TestSettingsWindowAsync();
        await TestFloatingStatusAndLayoutAsync();
        var provider = new CountingProvider();
        var service = new QuotaRefreshService(provider, new SilentLogger());
        var monitor = new QuotaMonitorCoordinator(service, TimeSpan.FromMinutes(1));
        using var viewModel = new MainWindowViewModel(monitor, new(true, true, true, true));
        try
        {
            var notifications = 0;
            monitor.RefreshFailed += (_, _) => notifications++;
            await monitor.StartAsync();
            await monitor.StartAsync();
            Check(provider.Reads == 1 && viewModel.StatusText == "读取失败", "one initial refresh without a visible window");
            Check(notifications == 1, "one failure notification per attempt");
            using var floating = new FloatingWindowViewModel(monitor, new FloatingWindowDisplaySettings());
            using var reopened = new MainWindowViewModel(monitor, new(true, true, true, true));
            Check(provider.Reads == 1 && reopened.StatusText == "读取失败", "opening either view does not fetch quotas");
            var changes = 0;
            viewModel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(viewModel.StatusText)) changes++; };
            foreach (var language in new[] { "en", "zh-HK", "zh-CN" })
            {
                ApplicationLocalizer.Apply(language);
                Check(viewModel.StatusText == AppText.Get("ReadFailed") && floating.AccountText == "Unknown",
                    "main status switches language while floating account stays English");
            }
            Check(changes == 3 && provider.Reads == 1, "language switch updates bindings without quota reads");
            // Exercise the real one-minute DispatcherTimer without shortening production bounds.
            await provider.SecondRead.Task.WaitAsync(TimeSpan.FromSeconds(75));
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Check(provider.Reads == 2 && viewModel.StatusText == "正常", "periodic refresh recovers after initial failure");
            Check(viewModel.FiveHourPercent == "75%" && reopened.FiveHourPercent == "75%" &&
                floating.FiveHourPercent == "75%", "both views receive the same state");
            viewModel.Dispose();
            await monitor.RefreshAsync();
            Check(provider.Reads == 3 && floating.FiveHourPercent == "75%", "floating view keeps updating after main view disposal");
            Check(notifications == 1, "successful refresh does not repeat failure notification");
        }
        finally { await monitor.DisposeAsync(); }
        await monitor.StartAsync();
        await monitor.RefreshAsync();
        Check(provider.Reads == 3, "disposed monitor never restarts");

        var floatingOnlyProvider = new CountingProvider();
        var floatingOnlyMonitor = new QuotaMonitorCoordinator(
            new QuotaRefreshService(floatingOnlyProvider, new SilentLogger()), TimeSpan.FromMinutes(1));
        try
        {
            using var floatingOnly = new FloatingWindowViewModel(floatingOnlyMonitor, new FloatingWindowDisplaySettings());
            await floatingOnlyMonitor.StartAsync();
            await floatingOnlyMonitor.RefreshAsync();
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Check(floatingOnlyProvider.Reads == 2 && floatingOnly.FiveHourPercent == "75%" &&
                floatingOnly.AccountText == "ChatGPT Plus", "floating view works without a main view model");
        }
        finally { await floatingOnlyMonitor.DisposeAsync(); }

        var pending = new BlockingProvider();
        var pendingMonitor = new QuotaMonitorCoordinator(new QuotaRefreshService(pending, new SilentLogger()),
            TimeSpan.FromMinutes(1));
        using var pendingMain = new MainWindowViewModel(pendingMonitor, new(true, true, true, true));
        using var pendingFloating = new FloatingWindowViewModel(pendingMonitor, new FloatingWindowDisplaySettings());
        var startup = pendingMonitor.StartAsync();
        await pending.Started.Task;
        Check(ReferenceEquals(pendingFloating.StatusBrush, Brushes.DodgerBlue) &&
            pendingFloating.StatusText == AppText.GetForLanguage("Refreshing", "en"),
            "floating status is blue during an outstanding read");
        pendingMain.Dispose();
        pendingFloating.Dispose();
        await pendingMonitor.DisposeAsync();
        await startup;
        Check(pending.Cancelled, "exit cancels and waits for outstanding read");
        await pendingMonitor.DisposeAsync();
    }

    private static async Task TestFloatingStatusAndLayoutAsync()
    {
        var arguments = Environment.GetCommandLineArgs();
        var renderIndex = Array.IndexOf(arguments, "--render-settings");
        var renderOutput = renderIndex >= 0 && renderIndex + 1 < arguments.Length
            ? Path.GetFullPath(arguments[renderIndex + 1]) : null;
        var snapshot = new QuotaSnapshot("codex", null,
            new CodexAccountInfo(CodexAuthenticationType.ChatGpt, "chatgpt", "plus"),
            new QuotaWindow("5H", 25, 75, 300, DateTimeOffset.Now.AddHours(1)),
            null, DateTimeOffset.Now);
        var provider = new SequencedProvider(
            QuotaReadResult.Failure("initial failure"),
            QuotaReadResult.Success(snapshot),
            QuotaReadResult.Failure("later failure"));
        var monitor = new QuotaMonitorCoordinator(new QuotaRefreshService(provider, new SilentLogger()),
            TimeSpan.FromMinutes(1));
        using var viewModel = new FloatingWindowViewModel(monitor, new FloatingWindowDisplaySettings());
        var window = new FloatingWindow(viewModel, new FloatingWindowSettings());
        try
        {
            var content = (FrameworkElement)window.Content;
            var dot = Descendants<Ellipse>(content).Single();
            var summaryRow = Descendants<Grid>(content).Single(element => element.Name == "SummaryRow");
            var resetRow = Descendants<Grid>(content).Single(element => element.Name == "ResetRow");
            var subscriptionRow = Descendants<StackPanel>(content)
                .Single(element => element.Name == "SubscriptionRow");
            content.Measure(new Size(window.Width, double.PositiveInfinity));
            content.Arrange(new Rect(0, 0, window.Width, content.DesiredSize.Height));
            content.UpdateLayout();
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Check(ReferenceEquals(dot.Fill, Brushes.Gray) &&
                viewModel.StatusText == AppText.GetForLanguage("Waiting", "en"),
                "floating status waits before the first read");
            if (renderOutput is not null)
                RenderWindowContent(window, Path.Combine(renderOutput, "floating-status-gray.png"));

            await monitor.StartAsync();
            Check(ReferenceEquals(dot.Fill, Brushes.Firebrick) &&
                viewModel.StatusText == AppText.GetForLanguage("ReadFailed", "en"),
                "floating status is red after a failure without data");
            if (renderOutput is not null)
                RenderWindowContent(window, Path.Combine(renderOutput, "floating-status-red.png"));
            await monitor.RefreshAsync();
            Check(ReferenceEquals(dot.Fill, Brushes.ForestGreen) &&
                viewModel.StatusText == AppText.GetForLanguage("Healthy", "en"),
                "floating status is green after a successful read");
            if (renderOutput is not null)
                RenderWindowContent(window, Path.Combine(renderOutput, "floating-status-green.png"));
            await monitor.RefreshAsync();
            Check(ReferenceEquals(dot.Fill, Brushes.DarkOrange) &&
                viewModel.StatusText == AppText.GetForLanguage("Stale", "en"),
                "floating status is orange when a failure retains the last snapshot");
            if (renderOutput is not null)
                RenderWindowContent(window, Path.Combine(renderOutput, "floating-status-orange.png"));

            var fullHeight = 0d;
            var minimalHeight = 0d;
            for (var mask = 0; mask < 32; mask++)
            {
                var display = new FloatingWindowDisplaySettings
                {
                    ShowFiveHourQuota = (mask & 1) != 0,
                    ShowWeeklyQuota = (mask & 2) != 0,
                    ShowLastRefreshTime = (mask & 4) != 0,
                    ShowResetTimes = (mask & 8) != 0,
                    ShowSubscription = (mask & 16) != 0,
                };
                viewModel.ApplyDisplaySettings(display);
                content.Measure(new Size(window.Width, double.PositiveInfinity));
                content.Arrange(new Rect(0, 0, window.Width, content.DesiredSize.Height));
                content.UpdateLayout();

                var bounds = dot.TransformToAncestor(content).TransformBounds(
                    new Rect(new Size(dot.ActualWidth, dot.ActualHeight)));
                Check(dot.Visibility == Visibility.Visible &&
                    dot.ActualWidth is >= 6 and <= 9 && dot.ActualHeight is >= 6 and <= 9 &&
                    bounds.Right > content.ActualWidth - 30 && bounds.Right < content.ActualWidth &&
                    bounds.Bottom > content.ActualHeight - 24 && bounds.Bottom < content.ActualHeight,
                    $"floating status dot stays in the bottom-right corner for display mask {mask}: " +
                    $"dot={dot.ActualWidth}x{dot.ActualHeight}, bounds={bounds}, content={content.ActualWidth}x{content.ActualHeight}");
                Check(viewModel.TopSummaryVisibility == ((mask & 7) == 0 ? Visibility.Collapsed : Visibility.Visible),
                    $"floating summary visibility matches display mask {mask}");

                FrameworkElement? lastRow = (mask & 16) != 0 ? subscriptionRow :
                    (mask & 8) != 0 ? resetRow :
                    (mask & 7) != 0 ? summaryRow : null;
                if (lastRow is not null)
                {
                    var rowBounds = lastRow.TransformToAncestor(content).TransformBounds(
                        new Rect(new Size(lastRow.ActualWidth, lastRow.ActualHeight)));
                    Check(bounds.Top < rowBounds.Bottom - 0.5 && bounds.Bottom > rowBounds.Top + 0.5 &&
                        bounds.Left > rowBounds.Right,
                        $"floating status dot shares the last visible row without covering it for display mask {mask}: " +
                        $"dot={bounds}, lastRow={rowBounds}");
                }
                else
                {
                    Check(content.DesiredSize.Height <= dot.ActualHeight + 20,
                        $"floating status dot has no dedicated row when all fields are hidden: " +
                        $"contentHeight={content.DesiredSize.Height}, dotHeight={dot.ActualHeight}");
                }

                if (mask == 0) minimalHeight = content.DesiredSize.Height;
                if (mask == 31) fullHeight = content.DesiredSize.Height;
                if (renderOutput is not null && mask is 0 or 1 or 8 or 16 or 31)
                    RenderWindowContent(window, Path.Combine(renderOutput, $"floating-layout-mask-{mask}.png"));
            }

            Check(minimalHeight < fullHeight, "floating window shrinks to the status dot when all fields are hidden");
        }
        finally
        {
            window.CloseForExit();
            await monitor.DisposeAsync();
        }
    }

    private static async Task TestSettingsWindowAsync()
    {
        // Load the production shared styles without starting the production Application.
        using var resources = typeof(StartupTests).Assembly.GetManifestResourceStream("AppResources.xaml")!;
        var document = XDocument.Load(resources);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary = new XElement(presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            document.Root!.Element(presentation + "Application.Resources")!.Elements());
        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(dictionary.ToString()));
        ApplicationLocalizer.Apply("zh-CN");

        var registry = new MemoryStartupStore();
        var arguments = Environment.GetCommandLineArgs();
        var renderIndex = Array.IndexOf(arguments, "--render-settings");
        var output = renderIndex >= 0 && renderIndex + 1 < arguments.Length
            ? Path.GetFullPath(arguments[renderIndex + 1]) : null;
        if (output is not null) Directory.CreateDirectory(output);
        try
        {
            foreach (var language in new[] { "en", "zh-HK", "zh-CN" })
            {
                ApplicationLocalizer.Apply(language);
                var saves = 0;
                var window = new SettingsWindow(new AppSettings { Language = language }, Service(registry), _ =>
                {
                    saves++;
                    return Task.CompletedTask;
                });
                try
                {
                    // ToolTip requires a live presentation source. Keep the test window off screen and inactive.
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = -32000;
                    window.Top = -32000;
                    window.ShowActivated = false;
                    window.Show();
                    var viewModel = (SettingsWindowViewModel)window.DataContext;
                    Check(!viewModel.StartupEnabled && viewModel.MinimizeToTray, "settings window initial startup values");
                    Check(window.Title == AppText.Get("SettingsTitle"), "localized settings title");
                    Check(viewModel.StartupStatus == AppText.Get("StartupMissing"), "localized startup status");
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(window.Width, window.Height));
                    content.Arrange(new Rect(0, 0, window.Width, window.Height));
                    content.UpdateLayout();
                    var tabs = Descendants<TabControl>(content).Single();
                    await TestSettingsHelpAsync(window, output, language);
                    Check(saves == 0, "help interactions never save settings");
                    if (output is not null)
                    {
                        RenderWindowContent(window, Path.Combine(output, $"settings-{language}.png"));
                        var scroll = Descendants<ScrollViewer>(content).FirstOrDefault(s => s.ScrollableHeight > 0);
                        scroll?.ScrollToEnd();
                        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                        RenderWindowContent(window, Path.Combine(output, $"settings-lower-{language}.png"));
                    }
                    tabs.SelectedIndex = 1;
                    await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                    if (output is not null) RenderWindowContent(window, Path.Combine(output, $"floating-settings-{language}.png"));
                    ApplicationLocalizer.Apply(language == "en" ? "zh-CN" : "en");
                    Check(window.Title == AppText.Get("SettingsTitle"), "settings title updates dynamically");
                }
                finally { window.Close(); }
                if (output is not null) await RenderMonitorWindowsAsync(language, output);
            }
            Check(registry.Writes == 0 && registry.Deletes == 0, "opening localized settings never writes startup entry");
        }
        finally { ApplicationLocalizer.Apply("zh-CN"); }
    }

    private static async Task TestSettingsHelpAsync(SettingsWindow window, string? output, string language)
    {
        var content = (FrameworkElement)window.Content;
        var tabs = Descendants<TabControl>(content).Single();
        var viewModel = (SettingsWindowViewModel)window.DataContext;
        var original = viewModel.CreateSettings();
        (string Name, string Key, string Label)[] help =
        [
            ("LanguageHelp", "LanguageHint", "Language"),
            ("StartupHelp", "StartupHint", "StartupEnabled"),
            ("MinimizeHelp", "StartupWindowHint", "MinimizeToTray"),
            ("RegisterHelp", "RegisterCurrentPathHelp", "RegisterCurrentPath"),
            ("NotificationsHelp", "NotificationsHelp", "NotificationsEnabled"),
            ("RefreshIntervalHelp", "RefreshIntervalHint", "RefreshInterval"),
            ("FloatingLockHelp", "DragHint", "LockFloatingHint"),
        ];
        using var source = new System.Windows.Interop.HwndSource(
            new System.Windows.Interop.HwndSourceParameters("Settings help keyboard test")
            { Width = 1, Height = 1, WindowStyle = 0 });
        foreach (var (name, key, label) in help)
        {
            tabs.SelectedIndex = name == "FloatingLockHelp" ? 1 : 0;
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            content.UpdateLayout();
            var button = (Button)window.FindName(name);
            Check((string)button.Content == "ⓘ" && button.Focusable && button.IsEnabled,
                $"{name} is a focusable help icon even when its option is disabled");
            Check(button.ActualWidth >= 24 && button.ActualHeight >= 24 && button.FocusVisualStyle is not null,
                $"{name} has a usable hit area and focus indicator");
            Check(ReferenceEquals(button.Style, window.Resources["HelpIconStyle"]), "help icons share one style");
            Check(System.Windows.Automation.AutomationProperties.GetName(button) == AppText.Get(label) &&
                System.Windows.Automation.AutomationProperties.GetHelpText(button) == AppText.Get(key),
                $"{name} exposes localized accessible text");
            var parent = button.Parent as Grid;
            if (parent is not null)
            {
                var option = parent.Children.OfType<CheckBox>().Single();
                var optionRight = option.TranslatePoint(new Point(option.ActualWidth, 0), parent).X;
                Check(button.TranslatePoint(new Point(), parent).X >= optionRight,
                    $"{name} does not overlap its setting label");
            }
            var tip = (ToolTip)button.ToolTip;
            tip.Opacity = 0; // Popups must not flash on the user's desktop during automated checks.
            Check(tip.MaxWidth == 300, "help tooltip width is bounded");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Check(tip.IsOpen && (string)tip.Content == AppText.Get(key),
                $"{name} opens its localized explanation: open={tip.IsOpen}, content={tip.Content}, tag={button.Tag}");
            Check(Descendants<TextBlock>(tip).Any(text => text.TextWrapping == TextWrapping.Wrap),
                "tooltip content wraps");
            Check(tip.ActualWidth > 0 && tip.ActualWidth <= 300 && tip.ActualHeight > 0,
                "tooltip lays out within the maximum width");
            if (output is not null)
            {
                tip.UpdateLayout();
                RenderHelpToolTip(tip, Path.Combine(output, $"help-{name}-{language}.png"));
            }
            ApplicationLocalizer.Apply(language == "en" ? "zh-CN" : "en");
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Check((string)tip.Content == AppText.Get(key) &&
                System.Windows.Automation.AutomationProperties.GetHelpText(button) == AppText.Get(key),
                "open tooltip and accessibility text follow application language");
            ApplicationLocalizer.Apply(language);
            foreach (var keyPress in new[] { System.Windows.Input.Key.Escape, System.Windows.Input.Key.Enter,
                         System.Windows.Input.Key.Space })
            {
                var keyEvent = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                    source, Environment.TickCount, keyPress)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                button.RaiseEvent(keyEvent);
                Check(keyEvent.Handled, "help keys are consumed before save or cancel");
                Check(tip.IsOpen == (keyPress == System.Windows.Input.Key.Enter),
                    "Escape closes help, Enter opens help, Space toggles help closed");
            }
        }
        Check(viewModel.CreateSettings() == original && !viewModel.RegisterCurrentPath,
            "help interactions leave every setting unchanged");
        foreach (var tabIndex in new[] { 0, 1 })
        {
            tabs.SelectedIndex = tabIndex;
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            var options = Descendants<CheckBox>(content).ToArray();
            Check(options.Length == (tabIndex == 0 ? 8 : 7), "all original settings checkboxes remain");
            foreach (var option in options)
            {
                var expression = System.Windows.Data.BindingOperations.GetBindingExpression(option,
                    System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty);
                Check(expression is not null, "every setting checkbox retains its binding");
                var property = typeof(SettingsWindowViewModel).GetProperty(expression!.ParentBinding.Path.Path)!;
                var initial = (bool)property.GetValue(viewModel)!;
                option.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, !initial);
                Check((bool)property.GetValue(viewModel)! == !initial, "checkbox edits still reach the settings draft");
                property.SetValue(viewModel, initial);
                Check(option.IsChecked == initial, "settings draft still updates its checkbox");
            }
        }
        tabs.SelectedIndex = 0;
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        var checkboxes = Descendants<CheckBox>(content).ToArray();
        var languagePicker = Descendants<ComboBox>(content).Single();
        languagePicker.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedValueProperty,
            language == "en" ? "zh-CN" : "en");
        Check(viewModel.Language == (string)languagePicker.SelectedValue && AppText.Language == language,
            "language selection updates only the draft until saved");
        viewModel.Language = language;
        var startup = checkboxes.Single(box => System.Windows.Data.BindingOperations.GetBindingExpression(
            box, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.ParentBinding.Path.Path == "StartupEnabled");
        startup.IsChecked = true;
        Check(viewModel.StartupEnabled, "startup checkbox still updates its view model");
        viewModel.StartupEnabled = false;
        var status = Descendants<TextBlock>(content).Single(text => System.Windows.Data.BindingOperations.GetBindingExpression(
            text, TextBlock.TextProperty)?.ParentBinding.Path.Path == "StartupStatus");
        var notice = Descendants<TextBlock>(content).Single(text => System.Windows.Data.BindingOperations.GetBindingExpression(
            text, TextBlock.TextProperty)?.ParentBinding.Path.Path == "WindowsControlNotice");
        var error = Descendants<TextBlock>(content).Single(text => System.Windows.Data.BindingOperations.GetBindingExpression(
            text, TextBlock.TextProperty)?.ParentBinding.Path.Path == "SaveError");
        Check(status.Visibility == Visibility.Visible && notice.Visibility == Visibility.Visible &&
            notice.Text == AppText.Get("WindowsControlNotice"), "startup status and Windows restriction remain inline");
        var previousStatus = viewModel.StartupStatus;
        viewModel.StartupStatus = AppText.Get("StartupMismatch");
        Check(status.Text == viewModel.StartupStatus, "startup status still updates inline");
        Check(error.Visibility == Visibility.Collapsed, "empty save error is collapsed");
        viewModel.SaveError = AppText.Get("SettingsSaveFailed");
        content.UpdateLayout();
        Check(error.Visibility == Visibility.Visible && error.Text == viewModel.SaveError,
            "save errors remain visible in the footer");
        viewModel.SaveError = "";
        Check(error.Visibility == Visibility.Collapsed, "cleared save error collapses again");
        viewModel.StartupStatus = previousStatus;
        var slider = Descendants<Slider>(content).Single();
        slider.Value = 17;
        Check(viewModel.RefreshIntervalMinutes == 17 && slider.Minimum == 1 && slider.Maximum == 60,
            "refresh interval binding and limits are unchanged");
        viewModel.RefreshIntervalMinutes = original.RefreshIntervalMinutes;
        Check(viewModel.CreateSettings() == original, "binding checks restore the original settings draft");
    }

    private static void RenderHelpToolTip(ToolTip tip, string path)
    {
        tip.Opacity = 1;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(tip.ActualWidth),
            (int)Math.Ceiling(tip.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(tip);
        tip.Opacity = 0;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static async Task RenderMonitorWindowsAsync(string language, string output)
    {
        ApplicationLocalizer.Apply(language);
        var monitor = new QuotaMonitorCoordinator(new QuotaRefreshService(new CountingProvider(), new SilentLogger()),
            TimeSpan.FromMinutes(3));
        using var viewModel = new MainWindowViewModel(monitor, new(true, true, true, true));
        var window = new MainWindow(viewModel);
        using var floatingViewModel = new FloatingWindowViewModel(monitor, new FloatingWindowDisplaySettings
        {
            ShowResetTimes = true, ShowSubscription = true,
        });
        var floating = new FloatingWindow(floatingViewModel, new FloatingWindowSettings());
        try
        {
            await monitor.StartAsync();
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            RenderWindowContent(window, Path.Combine(output, $"main-failure-{language}.png"));
            await monitor.RefreshAsync();
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            window.Width = window.MinWidth;
            RenderWindowContent(window, Path.Combine(output, $"main-{language}.png"));
            RenderWindowContent(floating, Path.Combine(output, $"floating-{language}.png"));
        }
        finally
        {
            window.CloseForExit();
            floating.CloseForExit();
            await monitor.DisposeAsync();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void RenderWindowContent(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        var height = double.IsNaN(window.Height) ? double.PositiveInfinity : window.Height;
        content.Measure(new Size(window.Width, height));
        if (double.IsPositiveInfinity(height)) height = content.DesiredSize.Height;
        content.Arrange(new Rect(0, 0, window.Width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.Width, (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected a rejected startup target.");
    }

    private sealed class MemoryStartupStore : IStartupRegistrationStore
    {
        public string? Value { get; set; }
        public bool SystemAllowsStartup { get; set; }
        public bool FailReads { get; set; }
        public bool FailWrites { get; set; }
        public int Writes { get; private set; }
        public int Deletes { get; private set; }
        public string? Read() => FailReads ? throw new UnauthorizedAccessException("simulated read failure") : Value;
        public void Write(string command)
        {
            if (FailWrites) throw new UnauthorizedAccessException("simulated write failure");
            Writes++;
            Value = command;
        }
        public void Delete()
        {
            if (FailWrites) throw new UnauthorizedAccessException("simulated delete failure");
            Deletes++;
            Value = null;
        }
    }

    private sealed class CountingProvider : IQuotaProvider
    {
        public int Reads { get; private set; }
        public TaskCompletionSource SecondRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<QuotaReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            if (Reads == 1) return Task.FromResult(QuotaReadResult.Failure("simulated startup network failure"));
            SecondRead.TrySetResult();
            return Task.FromResult(QuotaReadResult.Success(new QuotaSnapshot("codex", null,
                new CodexAccountInfo(CodexAuthenticationType.ChatGpt, "chatgpt", "plus"),
                new QuotaWindow("5H", 25, 75, 300, DateTimeOffset.Now.AddHours(1)), null, DateTimeOffset.Now)));
        }
    }

    private sealed class SequencedProvider(params QuotaReadResult[] results) : IQuotaProvider
    {
        private readonly Queue<QuotaReadResult> _results = new(results);

        public Task<QuotaReadResult> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_results.Dequeue());
    }

    private sealed class BlockingProvider : IQuotaProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cancelled { get; private set; }
        public async Task<QuotaReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
            return QuotaReadResult.Failure("unreachable");
        }
    }

    private sealed class SilentLogger : IAppLogger
    {
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
