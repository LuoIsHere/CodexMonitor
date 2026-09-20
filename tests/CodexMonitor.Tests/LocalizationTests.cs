using System.IO;
using System.Text.RegularExpressions;
using LuoIsHere.CodexMonitor.App.ViewModels;
using LuoIsHere.CodexMonitor.Core.Abstractions;
using LuoIsHere.CodexMonitor.Core.Formatting;
using LuoIsHere.CodexMonitor.Core.Localization;
using LuoIsHere.CodexMonitor.Core.Models;
using LuoIsHere.CodexMonitor.Infrastructure.Codex;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

internal static class LocalizationTests
{
    public static (string Name, Action Run)[] Cases =>
    [
        ("complete translations and matching format arguments", TestTranslations),
        ("language persistence and legacy fallback", TestLanguageSettings),
        ("language draft is inert until saved", TestLanguageDraft),
    ];

    private static void TestTranslations()
    {
        var english = AppText.GetStrings("en");
        foreach (var language in new[] { "zh-CN", "en", "zh-HK" })
        {
            var translated = AppText.GetStrings(language);
            Check(english.Keys.Order().SequenceEqual(translated.Keys.Order()), "translation keys match");
            foreach (var (key, value) in translated)
            {
                Check(!string.IsNullOrWhiteSpace(value), $"empty {language}/{key}");
                var placeholders = Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).Order();
                Check(placeholders.SequenceEqual(Regex.Matches(english[key], @"\{\d+\}").Select(m => m.Value).Order()),
                    $"format arguments match for {language}/{key}");
                _ = AppText.GetForLanguage(key, language, 3, "example");
            }
        }

        try
        {
            var account = new CodexAccountInfo(CodexAuthenticationType.SignedOut, null, null);
            foreach (var (language, expected) in new[] { ("zh-CN", "未登录"), ("en", "Not signed in"), ("zh-HK", "未登入") })
            {
                AppText.SetLanguage(language);
                Check(QuotaDisplayFormatter.FormatAccount(account) == expected, "localized account state");
                Check(QuotaDisplayFormatter.FormatAccount(account, "en") == "Not signed in", "floating account stays English");
                using var invalidResponse = System.Text.Json.JsonDocument.Parse("[]");
                var rejected = false;
                try { AccountResponseParser.Parse(invalidResponse.RootElement); }
                catch (FormatException exception)
                {
                    rejected = true;
                    Check(exception.Message == AppText.Get("AccountResponseInvalid"), "localized parser error");
                }
                Check(rejected, "malformed response is rejected");
            }
            Check(AppText.Get("Save") == "儲存" && AppText.Get("FloatingWindow") == "浮動視窗", "Hong Kong terminology");
        }
        finally { AppText.SetLanguage("zh-CN"); }
    }

    private static void TestLanguageSettings()
    {
        var previous = Environment.GetEnvironmentVariable("CODEX_MONITOR_HOME");
        var directory = Path.Combine(Path.GetTempPath(), "CodexMonitor.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CODEX_MONITOR_HOME", directory);
            var store = new JsonSettingsStore(new SilentLogger());
            Check(store.LoadAsync().GetAwaiter().GetResult().Language == "zh-CN", "default language");
            foreach (var language in new[] { "zh-CN", "en", "zh-HK" })
            {
                var original = new AppSettings { Language = language, RefreshIntervalMinutes = 17,
                    Startup = new StartupSettings { Enabled = true, MinimizeToTray = false } };
                store.SaveAsync(original).GetAwaiter().GetResult();
                var restored = store.LoadAsync().GetAwaiter().GetResult();
                Check(restored.Language == language && restored.RefreshIntervalMinutes == 17 && restored.Startup == original.Startup,
                    "language roundtrip preserves other settings");
            }
            foreach (var json in new[] { "{}", "{\"schemaVersion\":4}", "{\"language\":null}", "{\"language\":\"invalid\"}" })
            {
                File.WriteAllText(AppPaths.SettingsFile, json);
                Check(store.LoadAsync().GetAwaiter().GetResult().Language == "zh-CN", "legacy or invalid language fallback");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_MONITOR_HOME", previous);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void TestLanguageDraft()
    {
        var settings = new AppSettings();
        var viewModel = new SettingsWindowViewModel(settings) { Language = "zh-HK" };
        Check(AppText.Language == "zh-CN" && settings.Language == "zh-CN", "editing or cancelling leaves active language unchanged");
        Check(viewModel.CreateSettings().Language == "zh-HK", "save draft contains selected language");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class SilentLogger : IAppLogger
    {
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
