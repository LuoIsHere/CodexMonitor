namespace LuoIsHere.CodexMonitor.Core.Models;

public sealed record QuotaMonitorState(
    QuotaSnapshot? LastSuccessfulSnapshot,
    bool IsRefreshing,
    string? Error,
    DateTimeOffset? LastAttemptAt);

