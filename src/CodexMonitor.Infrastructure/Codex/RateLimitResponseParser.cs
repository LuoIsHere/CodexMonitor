using System.Text.Json;
using LuoIsHere.CodexMonitor.Core.Models;

namespace LuoIsHere.CodexMonitor.Infrastructure.Codex;

public static class RateLimitResponseParser
{
    public static QuotaSnapshot Parse(JsonElement result, DateTimeOffset? observedAt = null)
    {
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("rateLimits", out var rateLimits) ||
            rateLimits.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Codex 响应缺少 rateLimits 对象");
        }

        var primary = ParseWindow(GetProperty(rateLimits, "primary"));
        var secondary = ParseWindow(GetProperty(rateLimits, "secondary"));
        var (fiveHour, weekly) = ClassifyWindows(primary, secondary);

        return new QuotaSnapshot(
            GetString(rateLimits, "limitId"),
            GetString(rateLimits, "limitName"),
            CodexAccountInfo.Unknown(GetString(rateLimits, "planType")),
            fiveHour,
            weekly,
            observedAt ?? DateTimeOffset.Now);
    }

    private static (QuotaWindow? FiveHour, QuotaWindow? Weekly) ClassifyWindows(
        QuotaWindow? primary,
        QuotaWindow? secondary)
    {
        var windows = new[] { primary, secondary }.OfType<QuotaWindow>().ToArray();
        var fiveHour = windows.FirstOrDefault(window => IsDuration(window.WindowDurationMinutes, 300, 10));
        var weekly = windows.FirstOrDefault(window => IsDuration(window.WindowDurationMinutes, 10_080, 120));

        if (fiveHour is null && weekly is null &&
            primary is not null && secondary is not null &&
            primary.WindowDurationMinutes is null && secondary.WindowDurationMinutes is null)
        {
            fiveHour = primary;
            weekly = secondary;
        }

        return (
            fiveHour is null ? null : fiveHour with { Label = "5H" },
            weekly is null ? null : weekly with { Label = "WK" });
    }

    private static QuotaWindow? ParseWindow(JsonElement? element)
    {
        if (element is not JsonElement value || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var used = GetDouble(value, "usedPercent");
        double? normalizedUsed = used.HasValue ? Math.Clamp(used.Value, 0d, 100d) : null;
        double? remaining = normalizedUsed.HasValue ? 100d - normalizedUsed.Value : null;

        return new QuotaWindow(
            "unknown",
            normalizedUsed,
            remaining,
            GetInt(value, "windowDurationMins"),
            GetResetTime(value, "resetsAt"));
    }

    private static DateTimeOffset? GetResetTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.TryGetInt64(out var raw))
        {
            try
            {
                return raw > 100_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(raw)
                    : DateTimeOffset.FromUnixTimeSeconds(raw);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static bool IsDuration(int? actual, int expected, int tolerance)
        => actual.HasValue && Math.Abs(actual.Value - expected) <= tolerance;

    private static JsonElement? GetProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value : null;

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;

    private static double? GetDouble(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetDouble(out var result) ? result : null;
}
