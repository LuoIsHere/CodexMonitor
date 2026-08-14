using System.Globalization;
using LuoIsHere.CodexMonitor.Core.Models;

namespace LuoIsHere.CodexMonitor.Core.Formatting;

public static class QuotaDisplayFormatter
{
    public const string NoneText = "None";

    public static string FormatAccount(CodexAccountInfo? account)
    {
        if (account is null)
        {
            return "Unknown";
        }

        return account.AuthenticationType switch
        {
            CodexAuthenticationType.ChatGpt => $"ChatGPT {FormatPlan(account.PlanType)}",
            CodexAuthenticationType.ApiKey => "Token",
            CodexAuthenticationType.Token => "Token",
            CodexAuthenticationType.SignedOut => "Not signed in",
            CodexAuthenticationType.Other => string.IsNullOrWhiteSpace(account.RawAccountType)
                ? "Other"
                : account.RawAccountType,
            _ => string.IsNullOrWhiteSpace(account.PlanType)
                ? "Unknown"
                : $"ChatGPT {FormatPlan(account.PlanType)}",
        };
    }

    public static string FormatPercent(QuotaWindow? window)
    {
        return window?.RemainingPercent is double remaining
            ? $"{Math.Round(remaining, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%"
            : "--";
    }

    public static string FormatPercent(QuotaSnapshot? snapshot, QuotaWindow? window)
        => snapshot?.Account.SuppressQuotaDisplay == true ? NoneText : FormatPercent(window);

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

    public static string FormatCountdown(
        QuotaSnapshot? snapshot,
        QuotaWindow? window,
        DateTimeOffset now,
        bool includeSeconds)
        => snapshot?.Account.SuppressQuotaDisplay == true
            ? NoneText
            : FormatCountdown(window, now, includeSeconds);

    public static string FormatRefreshTime(QuotaSnapshot? snapshot)
    {
        if (snapshot?.Account.SuppressQuotaDisplay == true)
        {
            return NoneText;
        }

        return snapshot is null
            ? "--"
            : snapshot.ObservedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private static string FormatPlan(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType))
        {
            return "Unknown";
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(planType.Trim().ToLowerInvariant());
    }
}
