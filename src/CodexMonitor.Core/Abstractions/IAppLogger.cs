namespace LuoIsHere.CodexMonitor.Core.Abstractions;

public interface IAppLogger
{
    void Info(string message);

    void Error(string message, Exception? exception = null);
}

