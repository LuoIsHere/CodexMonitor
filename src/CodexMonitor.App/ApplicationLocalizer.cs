using LuoIsHere.CodexMonitor.Core.Localization;

namespace LuoIsHere.CodexMonitor.App;

public static class ApplicationLocalizer
{
    public static void Apply(string language)
    {
        var application = System.Windows.Application.Current;
        application.Dispatcher.VerifyAccess();
        foreach (var (key, value) in AppText.GetStrings(language))
        {
            application.Resources["Text." + key] = value;
        }
        AppText.SetLanguage(language);
    }
}
