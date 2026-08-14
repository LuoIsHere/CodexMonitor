namespace LuoIsHere.CodexMonitor.Core.Models;

public sealed record QuotaReadResult(QuotaSnapshot? Snapshot, string? Error)
{
    public bool IsSuccess => Snapshot is not null && string.IsNullOrWhiteSpace(Error);

    public static QuotaReadResult Success(QuotaSnapshot snapshot) => new(snapshot, null);

    public static QuotaReadResult Failure(string error) => new(null, error);
}

