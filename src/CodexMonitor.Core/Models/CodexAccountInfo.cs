namespace LuoIsHere.CodexMonitor.Core.Models;

public enum CodexAuthenticationType
{
    Unknown,
    ChatGpt,
    ApiKey,
    Token,
    Other,
    SignedOut,
}

public sealed record CodexAccountInfo(
    CodexAuthenticationType AuthenticationType,
    string? RawAccountType,
    string? PlanType)
{
    public static CodexAccountInfo Unknown(string? planType = null)
        => new(CodexAuthenticationType.Unknown, null, planType);

    public bool SuppressQuotaDisplay => AuthenticationType is
        CodexAuthenticationType.ApiKey or
        CodexAuthenticationType.Token or
        CodexAuthenticationType.Other or
        CodexAuthenticationType.SignedOut;
}
