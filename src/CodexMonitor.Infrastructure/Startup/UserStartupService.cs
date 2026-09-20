using LuoIsHere.CodexMonitor.Core.Localization;
namespace LuoIsHere.CodexMonitor.Infrastructure.Startup;

public sealed class UserStartupService
{
    public static string WindowsControlNotice => AppText.Get("WindowsControlNotice");
    private readonly IStartupRegistrationStore _store;
    private readonly StartupExecutableResolver _executable;

    public UserStartupService(IStartupRegistrationStore store, StartupExecutableResolver executable)
    {
        _store = store;
        _executable = executable;
    }

    public string ReadStatus()
    {
        try
        {
            var registered = _store.Read();
            if (registered is null)
            {
                return AppText.Get("StartupMissing");
            }

            var expected = CreateCommand();
            return string.Equals(registered, expected, StringComparison.OrdinalIgnoreCase)
                ? AppText.Get("StartupRegistered")
                : AppText.Get("StartupMismatch");
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return AppText.Get("StartupStatusFailed", exception.Message);
        }
    }

    // Call only from a confirmed, explicit startup preference edit.
    public void Apply(bool enabled)
    {
        var registered = _store.Read();
        if (enabled)
        {
            var command = CreateCommand();
            if (!string.Equals(registered, command, StringComparison.OrdinalIgnoreCase))
            {
                _store.Write(command);
            }
        }
        else if (registered is not null)
        {
            _store.Delete();
        }
    }

    private string CreateCommand()
    {
        var command = $"\"{_executable.Resolve()}\" --autostart";
        if (command.Length > 260)
        {
            throw new InvalidOperationException(AppText.Get("StartupCommandTooLong"));
        }

        return command;
    }

    internal static bool IsExpectedFailure(Exception exception)
        => exception is IOException or UnauthorizedAccessException or System.Security.SecurityException
            or InvalidOperationException or ArgumentException or NotSupportedException;
}
