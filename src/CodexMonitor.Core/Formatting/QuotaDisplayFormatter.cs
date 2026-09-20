using System.Globalization;
using LuoIsHere.CodexMonitor.Core.Localization;
using LuoIsHere.CodexMonitor.Core.Models;

namespace LuoIsHere.CodexMonitor.Core.Formatting;

public static class QuotaDisplayFormatter
{
    public const string NoneText = "None";

    public static string FormatAccount(CodexAccountInfo? account, string? language = null)
    {
        if (account is null)
        {
            return AppText.GetForLanguage("Unknown", language ?? AppText.Language);
        }

        return account.AuthenticationType switch
        {
            CodexAuthenticationType.ChatGpt => $"ChatGPT {FormatPlan(account.PlanType, language)}",
            CodexAuthenticationType.ApiKey => "Token",
            CodexAuthenticationType.Token => "Token",
            CodexAuthenticationType.SignedOut => AppText.GetForLanguage("SignedOut", language ?? AppText.Language),
            CodexAuthenticationType.Other => string.IsNullOrWhiteSpace(account.RawAccountType)
                ? AppText.GetForLanguage("Other", language ?? AppText.Language)
                : account.RawAccountType,
            _ => string.IsNullOrWhiteSpace(account.PlanType)
                ? AppText.GetForLanguage("Unknown", language ?? AppText.Language)
                : $"ChatGPT {FormatPlan(account.PlanType, language)}",
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
            return window is null ? AppText.Get("Unavailable") : "--";
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

    public static string FormatResetTime(QuotaSnapshot? snapshot, QuotaWindow? window)
    {
        if (snapshot?.Account.SuppressQuotaDisplay == true ||
            window?.ResetsAt is not DateTimeOffset resetsAt)
        {
            return "-";
        }

        return resetsAt.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.CurrentCulture);
    }

    private static string FormatPlan(string? planType, string? language)
    {
        if (string.IsNullOrWhiteSpace(planType))
        {
            return AppText.GetForLanguage("Unknown", language ?? AppText.Language);
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(planType.Trim().ToLowerInvariant());
    }
}
