using System.Globalization;
using LuoIsHere.CodexMonitor.Core.Models;

namespace LuoIsHere.CodexMonitor.Core.Formatting;

public static class QuotaDisplayFormatter
{
    public static string FormatPercent(QuotaWindow? window)
    {
        return window?.RemainingPercent is double remaining
            ? $"{Math.Round(remaining, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%"
            : "--";
    }

    public static string FormatCountdown(QuotaWindow? window, DateTimeOffset now, bool includeSeconds)
    {
        if (window?.ResetsAt is not DateTimeOffset resetsAt)
        {
            return window is null ? "unavailable" : "--";
        }

        var remaining = resetsAt - now;
        if (remaining <= TimeSpan.Zero)
        {
            return includeSeconds ? "00:00:00" : "0d 00h";
        }

        if (includeSeconds)
        {
            var totalHours = (int)Math.Floor(remaining.TotalHours);
            return $"{totalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        return $"{(int)Math.Floor(remaining.TotalDays)}d {remaining.Hours:00}h";
    }

    public static string FormatRefreshTime(QuotaSnapshot? snapshot)
    {
        return snapshot is null ? "--" : snapshot.ObservedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    }
}

