using System.Text.Json;
using LuoIsHere.CodexMonitor.Core.Abstractions;
using LuoIsHere.CodexMonitor.Core.Formatting;
using LuoIsHere.CodexMonitor.Core.Models;
using LuoIsHere.CodexMonitor.Infrastructure.Codex;
using LuoIsHere.CodexMonitor.Infrastructure.Settings;

var tests = new (string Name, Action Run)[]
{
    ("standard response", TestStandardResponse),
    ("swapped windows", TestSwappedWindows),
    ("legacy positional fallback", TestLegacyFallback),
    ("percent clamping", TestPercentClamping),
    ("milliseconds reset timestamp", TestMillisecondTimestamp),
    ("single weekly window", TestSingleWeeklyWindow),
    ("missing rate limits", TestMissingRateLimits),
    ("display formatting", TestDisplayFormatting),
    ("ChatGPT account response", TestChatGptAccountResponse),
    ("API key account response", TestApiKeyAccountResponse),
    ("signed-out account response", TestSignedOutAccountResponse),
    ("token display suppression", TestTokenDisplaySuppression),
    ("schema 1 settings migration", TestSchemaOneSettingsMigration),
    ("schema 2 settings migration", TestSchemaTwoSettingsMigration),
    ("schema 3 settings roundtrip", TestSchemaThreeSettingsRoundtrip),
};

if (args.Contains("--live", StringComparer.OrdinalIgnoreCase))
{
    return await RunLiveProbeAsync();
}

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"[PASS] {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"[FAIL] {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static void TestStandardResponse()
{
    var snapshot = Parse("""
        {
          "rateLimits": {
            "limitId": "codex",
            "planType": "plus",
            "primary": { "usedPercent": 28, "windowDurationMins": 300, "resetsAt": 1800000000 },
            "secondary": { "usedPercent": 57, "windowDurationMins": 10080, "resetsAt": 1800100000 }
          }
        }
        """);

    Equal(72d, snapshot.FiveHour?.RemainingPercent, "5H remaining");
    Equal(43d, snapshot.Weekly?.RemainingPercent, "weekly remaining");
    Equal("plus", snapshot.PlanType, "plan type");
}

static void TestSwappedWindows()
{
    var snapshot = Parse("""
        {
          "rateLimits": {
            "primary": { "usedPercent": 60, "windowDurationMins": 10080, "resetsAt": 1800100000 },
            "secondary": { "usedPercent": 10, "windowDurationMins": 300, "resetsAt": 1800000000 }
          }
        }
        """);

    Equal(90d, snapshot.FiveHour?.RemainingPercent, "swapped 5H");
    Equal(40d, snapshot.Weekly?.RemainingPercent, "swapped weekly");
}

static void TestLegacyFallback()
{
    var snapshot = Parse("""
        {
          "rateLimits": {
            "primary": { "usedPercent": 20 },
            "secondary": { "usedPercent": 30 }
          }
        }
        """);

    Equal(80d, snapshot.FiveHour?.RemainingPercent, "legacy 5H");
    Equal(70d, snapshot.Weekly?.RemainingPercent, "legacy weekly");
}

static void TestPercentClamping()
{
    var snapshot = Parse("""
        {
          "rateLimits": {
            "primary": { "usedPercent": 120, "windowDurationMins": 300 },
            "secondary": { "usedPercent": -2, "windowDurationMins": 10080 }
          }
        }
        """);

    Equal(0d, snapshot.FiveHour?.RemainingPercent, "upper clamp");
    Equal(100d, snapshot.Weekly?.RemainingPercent, "lower clamp");
}

static void TestMillisecondTimestamp()
{
    var snapshot = Parse("""
        {
          "rateLimits": {
            "primary": { "usedPercent": 25, "windowDurationMins": 300, "resetsAt": 1800000000000 }
          }
        }
        """);

    Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000), snapshot.FiveHour?.ResetsAt, "milliseconds");
}

static void TestSingleWeeklyWindow()
{
    var snapshot = Parse("""
        {
          "rateLimits": {
            "limitId": "codex",
            "primary": { "usedPercent": 15, "windowDurationMins": 10080, "resetsAt": 1800100000 },
            "secondary": null,
            "planType": "plus"
          }
        }
        """);

    Equal(null, snapshot.FiveHour, "missing 5H window");
    Equal(85d, snapshot.Weekly?.RemainingPercent, "single weekly remaining");
}

static void TestMissingRateLimits()
{
    try
    {
        _ = Parse("{\"unexpected\":true}");
        throw new InvalidOperationException("Expected FormatException was not thrown.");
    }
    catch (FormatException)
    {
    }
}

static void TestDisplayFormatting()
{
    var snapshot = Parse("""
        {
          "rateLimits": {
            "primary": { "usedPercent": 28.4, "windowDurationMins": 300, "resetsAt": 1800000000 }
          }
        }
        """);

    Equal("72%", QuotaDisplayFormatter.FormatPercent(snapshot.FiveHour), "rounded percent");
    Equal("unavailable", QuotaDisplayFormatter.FormatCountdown(null, DateTimeOffset.Now, true), "unavailable window");
    Equal("-", QuotaDisplayFormatter.FormatResetTime(snapshot, null), "missing reset time");
    Equal(
        snapshot.FiveHour!.ResetsAt!.Value.ToLocalTime().ToString("MM-dd HH:mm"),
        QuotaDisplayFormatter.FormatResetTime(snapshot, snapshot.FiveHour),
        "formatted reset time");
}

static void TestChatGptAccountResponse()
{
    var account = ParseAccount("""
        {
          "account": {
            "type": "chatgpt",
            "email": "ignored@example.com",
            "planType": "pro"
          },
          "requiresOpenaiAuth": true
        }
        """);

    Equal(CodexAuthenticationType.ChatGpt, account.AuthenticationType, "ChatGPT authentication type");
    Equal("pro", account.PlanType, "ChatGPT plan type");
    Equal("ChatGPT Pro", QuotaDisplayFormatter.FormatAccount(account), "ChatGPT account display");
    Equal(false, account.SuppressQuotaDisplay, "ChatGPT quota visibility");
}

static void TestApiKeyAccountResponse()
{
    var account = ParseAccount("""
        {
          "account": { "type": "apiKey" },
          "requiresOpenaiAuth": true
        }
        """);

    Equal(CodexAuthenticationType.ApiKey, account.AuthenticationType, "API key authentication type");
    Equal("Token", QuotaDisplayFormatter.FormatAccount(account), "API key account display");
    Equal(true, account.SuppressQuotaDisplay, "API key quota suppression");
}

static void TestSignedOutAccountResponse()
{
    var account = ParseAccount("""
        {
          "account": null,
          "requiresOpenaiAuth": true
        }
        """);

    Equal(CodexAuthenticationType.SignedOut, account.AuthenticationType, "signed-out authentication type");
    Equal("Not signed in", QuotaDisplayFormatter.FormatAccount(account), "signed-out account display");
}

static void TestTokenDisplaySuppression()
{
    var account = ParseAccount("""
        {
          "account": {
            "type": "chatgptAuthTokens",
            "planType": "plus"
          },
          "requiresOpenaiAuth": true
        }
        """);
    var window = new QuotaWindow(
        "5H",
        25,
        75,
        300,
        DateTimeOffset.Now.AddHours(2));
    var snapshot = new QuotaSnapshot(
        "codex",
        null,
        account,
        window,
        window with { Label = "WK" },
        DateTimeOffset.Now);

    Equal(CodexAuthenticationType.Token, account.AuthenticationType, "token authentication type");
    Equal("Token", QuotaDisplayFormatter.FormatAccount(account), "token account display");
    Equal("None", QuotaDisplayFormatter.FormatPercent(snapshot, snapshot.FiveHour), "token remaining quota");
    Equal(
        "None",
        QuotaDisplayFormatter.FormatCountdown(snapshot, snapshot.FiveHour, DateTimeOffset.Now, true),
        "token reset countdown");
    Equal("None", QuotaDisplayFormatter.FormatRefreshTime(snapshot), "token refresh time");
    Equal("-", QuotaDisplayFormatter.FormatResetTime(snapshot, snapshot.FiveHour), "token reset time");
}

static void TestSchemaOneSettingsMigration()
{
    WithTemporarySettingsDirectory(directory =>
    {
        File.WriteAllText(
            Path.Combine(directory, "settings.json"),
            """
            {
              "schemaVersion": 1,
              "refreshIntervalMinutes": 0,
              "codexExecutable": null
            }
            """);

        var settings = new JsonSettingsStore(new SilentLogger()).LoadAsync().GetAwaiter().GetResult();
        Equal(3, settings.SchemaVersion, "migrated schema version");
        Equal(1, settings.RefreshIntervalMinutes, "clamped legacy refresh interval");
        Equal(true, settings.Notifications.Enabled, "legacy notification default");
        Equal(true, settings.Display.ShowFiveHourQuota, "legacy 5H display default");
        Equal(true, settings.Display.ShowWeeklyQuota, "legacy 7D display default");
        Equal(true, settings.Display.ShowResetTimes, "legacy reset display default");
        Equal(true, settings.Display.ShowSubscription, "legacy subscription display default");
        Equal(false, settings.FloatingWindow.Enabled, "legacy floating window default");
        Equal(true, settings.FloatingWindow.Display.ShowFiveHourQuota, "legacy floating 5H default");
        Equal(true, settings.FloatingWindow.Display.ShowWeeklyQuota, "legacy floating 7D default");
        Equal(true, settings.FloatingWindow.Display.ShowLastRefreshTime, "legacy floating refresh time default");
    });
}

static void TestSchemaTwoSettingsMigration()
{
    WithTemporarySettingsDirectory(directory =>
    {
        File.WriteAllText(
            Path.Combine(directory, "settings.json"),
            """
            {
              "schemaVersion": 2,
              "refreshIntervalMinutes": 9,
              "notifications": { "enabled": false },
              "display": {
                "showFiveHourQuota": false,
                "showWeeklyQuota": true,
                "showResetTimes": false,
                "showSubscription": true
              }
            }
            """);

        var settings = new JsonSettingsStore(new SilentLogger()).LoadAsync().GetAwaiter().GetResult();
        Equal(3, settings.SchemaVersion, "schema 2 migrated version");
        Equal(9, settings.RefreshIntervalMinutes, "schema 2 refresh interval");
        Equal(false, settings.Notifications.Enabled, "schema 2 notification state");
        Equal(false, settings.Display.ShowFiveHourQuota, "schema 2 main display state");
        Equal(false, settings.FloatingWindow.Enabled, "schema 2 floating window default");
        Equal(true, settings.FloatingWindow.Display.ShowLastRefreshTime, "schema 2 floating refresh time default");
    });
}

static void TestSchemaThreeSettingsRoundtrip()
{
    WithTemporarySettingsDirectory(_ =>
    {
        var expected = new AppSettings
        {
            RefreshIntervalMinutes = 17,
            Notifications = new NotificationSettings { Enabled = false },
            Display = new DisplaySettings
            {
                ShowFiveHourQuota = false,
                ShowWeeklyQuota = true,
                ShowResetTimes = false,
                ShowSubscription = false,
            },
            FloatingWindow = new FloatingWindowSettings
            {
                Enabled = true,
                IsLocked = true,
                Left = 123.5,
                Top = 456.25,
                Display = new FloatingWindowDisplaySettings
                {
                    ShowFiveHourQuota = true,
                    ShowWeeklyQuota = false,
                    ShowLastRefreshTime = false,
                    ShowResetTimes = true,
                    ShowSubscription = true,
                },
            },
        };
        var store = new JsonSettingsStore(new SilentLogger());
        store.SaveAsync(expected).GetAwaiter().GetResult();
        var actual = store.LoadAsync().GetAwaiter().GetResult();

        Equal(3, actual.SchemaVersion, "saved schema version");
        Equal(17, actual.RefreshIntervalMinutes, "saved refresh interval");
        Equal(false, actual.Notifications.Enabled, "saved notification state");
        Equal(false, actual.Display.ShowFiveHourQuota, "saved 5H state");
        Equal(true, actual.Display.ShowWeeklyQuota, "saved 7D state");
        Equal(false, actual.Display.ShowResetTimes, "saved reset state");
        Equal(false, actual.Display.ShowSubscription, "saved subscription state");
        Equal(true, actual.FloatingWindow.Enabled, "saved floating window state");
        Equal(true, actual.FloatingWindow.IsLocked, "saved floating lock state");
        Equal(123.5, actual.FloatingWindow.Left, "saved floating left position");
        Equal(456.25, actual.FloatingWindow.Top, "saved floating top position");
        Equal(true, actual.FloatingWindow.Display.ShowResetTimes, "saved floating reset state");
        Equal(true, actual.FloatingWindow.Display.ShowSubscription, "saved floating subscription state");
    });
}

static void WithTemporarySettingsDirectory(Action<string> action)
{
    var previousHome = Environment.GetEnvironmentVariable("CODEX_MONITOR_HOME");
    var directory = Path.Combine(
        Path.GetTempPath(),
        "CodexMonitor.Tests",
        Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    Environment.SetEnvironmentVariable("CODEX_MONITOR_HOME", directory);

    try
    {
        action(directory);
    }
    finally
    {
        Environment.SetEnvironmentVariable("CODEX_MONITOR_HOME", previousHome);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

static LuoIsHere.CodexMonitor.Core.Models.QuotaSnapshot Parse(string json)
{
    using var document = JsonDocument.Parse(json);
    return RateLimitResponseParser.Parse(document.RootElement, DateTimeOffset.UnixEpoch);
}

static CodexAccountInfo ParseAccount(string json)
{
    using var document = JsonDocument.Parse(json);
    return AccountResponseParser.Parse(document.RootElement);
}

static void Equal<T>(T expected, T actual, string description)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{description}: expected '{expected}', actual '{actual}'");
    }
}

static async Task<int> RunLiveProbeAsync()
{
    var logger = new ConsoleLogger();
    var provider = new CodexAppServerQuotaProvider(new CodexExecutableLocator(), null, logger);
    var result = await provider.ReadAsync();
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return result.IsSuccess ? 0 : 1;
}

file sealed class ConsoleLogger : IAppLogger
{
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");

    public void Error(string message, Exception? exception = null)
        => Console.Error.WriteLine($"[ERROR] {message} {exception?.Message}");
}

file sealed class SilentLogger : IAppLogger
{
    public void Info(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
