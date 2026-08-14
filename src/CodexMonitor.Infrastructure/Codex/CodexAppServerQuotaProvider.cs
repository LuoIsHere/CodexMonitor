using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
        var stage = "启动 Codex app-server";
        var executable = _locator.Find(_configuredExecutable);
        if (string.IsNullOrWhiteSpace(executable))
        {
            return QuotaReadResult.Failure("未找到 codex.exe；请确认 Codex 桌面版已安装");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            var result = await CallAppServerAsync(
                    executable,
                    currentStage => stage = currentStage,
                    timeout.Token)
                .ConfigureAwait(false);
            return QuotaReadResult.Success(RateLimitResponseParser.Parse(result));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return QuotaReadResult.Failure($"读取 Codex 用量超时（{stage}）");
        }
        catch (OperationCanceledException)
        {
            return QuotaReadResult.Failure("读取已取消");
        }
        catch (Exception exception)
        {
            _logger.Error("Codex app-server request failed.", exception);
            return QuotaReadResult.Failure(CompactError(exception));
        }
    }

    private static async Task<JsonElement> CallAppServerAsync(
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
            ?? throw new InvalidOperationException("无法启动 codex.exe app-server");

        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            reportStage("初始化");
            await SendRequestAsync(process, 1, "initialize", new
            {
                clientInfo = new { name = "codex-monitor", version = ClientVersion },
                capabilities = new
                {
                    experimentalApi = true,
                    optOutNotificationMethods = Array.Empty<string>(),
                },
            }, cancellationToken).ConfigureAwait(false);
            await ReadResponseAsync(process, 1, cancellationToken).ConfigureAwait(false);

            await SendNotificationAsync(process, "initialized", cancellationToken).ConfigureAwait(false);

            reportStage("读取额度");
            await SendRequestAsync(process, 2, "account/rateLimits/read", null, cancellationToken)
                .ConfigureAwait(false);
            var response = await ReadResponseAsync(process, 2, cancellationToken).ConfigureAwait(false);

            if (!response.TryGetProperty("result", out var result))
            {
                throw new InvalidOperationException("Codex JSON-RPC 响应缺少 result");
            }

            reportStage("解析响应");
            return result.Clone();
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

    private static async Task<JsonElement> ReadResponseAsync(
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
                    throw new InvalidOperationException($"codex.exe app-server 已退出，代码 {process.ExitCode}");
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

                if (root.TryGetProperty("error", out var error))
                {
                    throw new InvalidOperationException($"Codex JSON-RPC 错误：{error.GetRawText()}");
                }

                return root.Clone();
            }
        }
    }

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
            UnauthorizedAccessException => "无权启动 Codex app-server",
            FileNotFoundException => "codex.exe 已不存在",
            FormatException => exception.Message,
            InvalidOperationException => exception.Message.Length <= 180
                ? exception.Message
                : exception.Message[..180] + "…",
            _ => "读取 Codex 用量失败",
        };
    }
}
