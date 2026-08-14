using System.Text.Json;
using LuoIsHere.CodexMonitor.Core.Abstractions;
using LuoIsHere.CodexMonitor.Core.Formatting;
using LuoIsHere.CodexMonitor.Core.Models;
using LuoIsHere.CodexMonitor.Infrastructure.Codex;

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
