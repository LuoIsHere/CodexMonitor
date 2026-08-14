using System.Text.Json;
using LuoIsHere.CodexMonitor.Core.Models;

namespace LuoIsHere.CodexMonitor.Infrastructure.Codex;

public static class AccountResponseParser
{
    public static CodexAccountInfo Parse(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Codex 账号响应不是对象");
        }

        if (!result.TryGetProperty("account", out var account) ||
            account.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return GetBoolean(result, "requiresOpenaiAuth") switch
            {
                true => new CodexAccountInfo(CodexAuthenticationType.SignedOut, null, null),
                false => new CodexAccountInfo(CodexAuthenticationType.Other, null, null),
                null => CodexAccountInfo.Unknown(),
            };
        }

        if (account.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Codex 账号响应中的 account 不是对象");
        }

        var rawType = GetString(account, "type");
        var planType = GetString(account, "planType");
        var authenticationType = rawType switch
        {
            "chatgpt" => CodexAuthenticationType.ChatGpt,
            "apiKey" => CodexAuthenticationType.ApiKey,
            "chatgptAuthTokens" or "personalAccessToken" => CodexAuthenticationType.Token,
            null or "" => CodexAuthenticationType.Unknown,
            _ => CodexAuthenticationType.Other,
        };

        return new CodexAccountInfo(authenticationType, rawType, planType);
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? GetBoolean(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
