namespace LuoIsHere.CodexMonitor.App;

public interface IFloatingWindowService
{
    bool IsAvailable { get; }

    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

internal sealed class UnavailableFloatingWindowService : IFloatingWindowService
{
    public bool IsAvailable => false;

    public bool IsEnabled => false;

    public void SetEnabled(bool enabled)
    {
        // The contract is reserved for a later floating-window implementation.
    }
}
