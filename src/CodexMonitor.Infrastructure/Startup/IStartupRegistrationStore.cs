namespace LuoIsHere.CodexMonitor.Infrastructure.Startup;

// A store owns only this application's startup entry, never the surrounding key.
public interface IStartupRegistrationStore
{
    string? Read();

    void Write(string command);

    void Delete();
}
