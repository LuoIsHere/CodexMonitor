using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LuoIsHere.CodexMonitor.Core.Localization;
using LuoIsHere.CodexMonitor.Core.Abstractions;
using LuoIsHere.CodexMonitor.Core.Models;

namespace LuoIsHere.CodexMonitor.Infrastructure.Codex;

public sealed class CodexAppServerQuotaProvider : IQuotaProvider
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly string ClientVersion =
        typeof(CodexAppServerQuotaProvider).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    private readonly CodexExecutableLocator _locator;
    private readonly string? _configuredExecutable;
    private readonly IAppLogger _logger;

    public CodexAppServerQuotaProvider(
        CodexExecutableLocator locator,
        string? configuredExecutable,
        IAppLogger logger)
    {
        _locator = locator;
        _configuredExecutable = configuredExecutable;
        _logger = logger;
    }

    public async Task<QuotaReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var stage = AppText.Get("StageStart");
        var executable = _locator.Find(_configuredExecutable);
        if (string.IsNullOrWhiteSpace(executable))
        {
            return QuotaReadResult.Failure(AppText.Get("CodexNotFound"));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            var snapshot = await CallAppServerAsync(
                    executable,
                    currentStage => stage = currentStage,
                    timeout.Token)
                .ConfigureAwait(false);
            return QuotaReadResult.Success(snapshot);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return QuotaReadResult.Failure(AppText.Get("ReadTimeout", stage));
        }
        catch (OperationCanceledException)
        {
            return QuotaReadResult.Failure(AppText.Get("ReadCancelled"));
        }
        catch (Exception exception)
        {
            _logger.Error("Codex app-server request failed.", exception);
            return QuotaReadResult.Failure(CompactError(exception));
        }
    }

    private static async Task<QuotaSnapshot> CallAppServerAsync(
        string executable,
        Action<string> reportStage,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8WithoutBom,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom,
        };
        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--listen");
        startInfo.ArgumentList.Add("stdio://");

        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            startInfo.Environment["CODEX_HOME"] = codexHome;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(AppText.Get("CodexStartFailed"));

        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            reportStage(AppText.Get("StageInitialize"));
            await SendRequestAsync(process, 1, "initialize", new
            {
                clientInfo = new { name = "codex-monitor", version = ClientVersion },
                capabilities = new
                {
                    experimentalApi = true,
                    optOutNotificationMethods = Array.Empty<string>(),
                },
            }, cancellationToken).ConfigureAwait(false);
            _ = await ReadRequiredResultAsync(process, 1, cancellationToken).ConfigureAwait(false);

            await SendNotificationAsync(process, "initialized", cancellationToken).ConfigureAwait(false);

            reportStage(AppText.Get("StageAccount"));
            var account = await TryReadAccountAsync(process, 2, cancellationToken).ConfigureAwait(false);
            if (account?.SuppressQuotaDisplay == true)
            {
                reportStage(AppText.Get("StageParse"));
                return new QuotaSnapshot(
                    null,
                    null,
                    account,
                    null,
                    null,
                    DateTimeOffset.Now);
            }

            reportStage(AppText.Get("StageQuota"));
            await SendRequestAsync(process, 3, "account/rateLimits/read", null, cancellationToken)
                .ConfigureAwait(false);
            var result = await ReadRequiredResultAsync(process, 3, cancellationToken).ConfigureAwait(false);

            reportStage(AppText.Get("StageParse"));
            var snapshot = RateLimitResponseParser.Parse(result);
            if (account is not null)
            {
                account = account with { PlanType = account.PlanType ?? snapshot.PlanType };
                snapshot = snapshot with { Account = account };
            }

            return snapshot;
        }
        finally
        {
            StopProcess(process);
            try
            {
                _ = await standardErrorTask.ConfigureAwait(false);
            }
            catch
            {
                // The process can be terminated while stderr is still being read.
            }
        }
    }

    private static async Task<CodexAccountInfo?> TryReadAccountAsync(
        Process process,
        int id,
        CancellationToken cancellationToken)
    {
        await SendRequestAsync(
                process,
                id,
                "account/read",
                new { refreshToken = false },
                cancellationToken)
            .ConfigureAwait(false);

        var response = await ReadResponseMessageAsync(process, id, cancellationToken).ConfigureAwait(false);
        if (response.TryGetProperty("error", out var error))
        {
            if (TryGetRpcErrorCode(error) == -32601)
            {
                return null;
            }

            throw CreateRpcException(error);
        }

        if (!response.TryGetProperty("result", out var result))
        {
            throw new InvalidOperationException(AppText.Get("AccountResultMissing"));
        }

        return AccountResponseParser.Parse(result);
    }

    private static async Task SendRequestAsync(
        Process process,
        int id,
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id,
            method,
            @params = parameters,
        });

        await process.StandardInput.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SendNotificationAsync(
        Process process,
        string method,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { method });
        await process.StandardInput.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonElement> ReadRequiredResultAsync(
        Process process,
        int expectedId,
        CancellationToken cancellationToken)
    {
        var response = await ReadResponseMessageAsync(process, expectedId, cancellationToken).ConfigureAwait(false);
        if (response.TryGetProperty("error", out var error))
        {
            throw CreateRpcException(error);
        }

        if (!response.TryGetProperty("result", out var result))
        {
            throw new InvalidOperationException(AppText.Get("RpcResultMissing"));
        }

        return result.Clone();
    }

    private static async Task<JsonElement> ReadResponseMessageAsync(
        Process process,
        int expectedId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(AppText.Get("CodexExited", process.ExitCode));
                }

                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                if (!root.TryGetProperty("id", out var id) || !id.TryGetInt32(out var actualId) || actualId != expectedId)
                {
                    continue;
                }

                return root.Clone();
            }
        }
    }

    private static int? TryGetRpcErrorCode(JsonElement error)
        => error.ValueKind == JsonValueKind.Object &&
           error.TryGetProperty("code", out var code) &&
           code.TryGetInt32(out var value)
            ? value
            : null;

    private static InvalidOperationException CreateRpcException(JsonElement error)
        => new(AppText.Get("RpcError", error.GetRawText()));

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2_000);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static string CompactError(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => AppText.Get("CodexUnauthorized"),
            FileNotFoundException => AppText.Get("CodexMissing"),
            FormatException => exception.Message,
            InvalidOperationException => exception.Message.Length <= 180
                ? exception.Message
                : exception.Message[..180] + "…",
            _ => AppText.Get("CodexReadFailed"),
        };
    }
}
