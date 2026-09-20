namespace LuoIsHere.CodexMonitor.Infrastructure.Startup;

public sealed class UserStartupService
{
    public const string WindowsControlNotice = "仍受 Windows 启动应用设置控制；如已被系统禁用，请自行在 Windows 设置中确认。";
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
                return "未登记启动项。";
            }

            var expected = CreateCommand();
            return string.Equals(registered, expected, StringComparison.OrdinalIgnoreCase)
                ? "已登记当前应用路径。"
                : "登记路径或参数与当前应用不一致；可主动重新登记当前路径。";
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return $"无法确认启动项状态：{exception.Message}";
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
            throw new InvalidOperationException("启动命令超过 Windows Run 的 260 字符限制，请将应用移动到较短路径。");
        }

        return command;
    }

    internal static bool IsExpectedFailure(Exception exception)
        => exception is IOException or UnauthorizedAccessException or System.Security.SecurityException
            or InvalidOperationException or ArgumentException or NotSupportedException;
}
