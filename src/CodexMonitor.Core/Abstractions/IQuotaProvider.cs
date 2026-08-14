using LuoIsHere.CodexMonitor.Core.Models;

namespace LuoIsHere.CodexMonitor.Core.Abstractions;

public interface IQuotaProvider
{
    Task<QuotaReadResult> ReadAsync(CancellationToken cancellationToken = default);
}

