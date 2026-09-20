using System.Collections;
using System.Globalization;
using System.Resources;

namespace LuoIsHere.CodexMonitor.Core.Localization;

public static class AppText
{
    private static readonly ResourceManager Resources = new("CodexMonitor.Core.Localization.Strings", typeof(AppText).Assembly);
    private static volatile string _language = "zh-CN";

    public static event EventHandler? LanguageChanged;

    public static string Language => _language;

    public static string NormalizeLanguage(string? language) => language switch
    {
        "en" => "en",
        "zh-HK" => "zh-HK",
        _ => "zh-CN",
    };

    // Called by the UI thread after loading or successfully saving settings.
    public static void SetLanguage(string? language)
    {
        var normalized = NormalizeLanguage(language);
        if (_language == normalized) return;
        _language = normalized;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key, params object[] arguments)
        => GetForLanguage(key, Language, arguments);

    public static string GetForLanguage(string key, string language, params object[] arguments)
    {
        var culture = CultureInfo.GetCultureInfo(NormalizeLanguage(language));
        var value = Resources.GetString(key, culture) ?? throw new ArgumentException($"Missing translation: {key}", nameof(key));
        return arguments.Length == 0 ? value : string.Format(culture, value, arguments);
    }

    public static IReadOnlyDictionary<string, string> GetStrings(string language)
    {
        var culture = language == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(NormalizeLanguage(language));
        var set = Resources.GetResourceSet(culture, true, false)!;
        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }
}
