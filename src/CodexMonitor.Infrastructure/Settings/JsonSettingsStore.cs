using System.Text.Json;
using LuoIsHere.CodexMonitor.Core.Localization;
using LuoIsHere.CodexMonitor.Core.Abstractions;

namespace LuoIsHere.CodexMonitor.Infrastructure.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IAppLogger _logger;

    public JsonSettingsStore(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.StateDirectory);

        if (!File.Exists(AppPaths.SettingsFile))
        {
            var defaults = new AppSettings();
            await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(AppPaths.SettingsFile);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? new AppSettings();
            return Normalize(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.Error("Settings file could not be read; defaults will be used.", exception);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.StateDirectory);
        var normalized = Normalize(settings);
        var temporaryFile = AppPaths.SettingsFile + ".tmp";

        await using (var stream = new FileStream(
                         temporaryFile,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 4096,
                         useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporaryFile, AppPaths.SettingsFile, overwrite: true);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        return settings with
        {
            SchemaVersion = 4,
            Language = AppText.NormalizeLanguage(settings.Language),
            Startup = settings.SchemaVersion < 4 ? new StartupSettings() : settings.Startup ?? new StartupSettings(),
            RefreshIntervalMinutes = Math.Clamp(settings.RefreshIntervalMinutes, 1, 60),
            CodexExecutable = string.IsNullOrWhiteSpace(settings.CodexExecutable)
                ? null
                : Environment.ExpandEnvironmentVariables(settings.CodexExecutable.Trim()),
            Notifications = settings.Notifications ?? new NotificationSettings(),
            Display = settings.Display ?? new DisplaySettings(),
            FloatingWindow = NormalizeFloatingWindow(settings.FloatingWindow),
        };
    }

    private static FloatingWindowSettings NormalizeFloatingWindow(FloatingWindowSettings? settings)
    {
        settings ??= new FloatingWindowSettings();
        return settings with
        {
            Left = NormalizeCoordinate(settings.Left),
            Top = NormalizeCoordinate(settings.Top),
            Display = settings.Display ?? new FloatingWindowDisplaySettings(),
        };
    }

    private static double? NormalizeCoordinate(double? coordinate)
        => coordinate is double value && double.IsFinite(value)
            ? value
            : null;
}
