using System.Diagnostics;

namespace LuoIsHere.CodexMonitor.Infrastructure.Codex;

public sealed class CodexExecutableLocator
{
    public string? Find(string? configuredPath = null)
    {
        foreach (var candidate in GetCandidates(configuredPath))
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return FindUsingWhere();
    }

    private static IEnumerable<string> GetCandidates(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        }

        var environmentPath = Environment.GetEnvironmentVariable("CODEX_EXE");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            yield return Environment.ExpandEnvironmentVariables(environmentPath.Trim());
        }

        var desktopBinRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenAI",
            "Codex",
            "bin");
        if (Directory.Exists(desktopBinRoot))
        {
            foreach (var directory in Directory
                         .EnumerateDirectories(desktopBinRoot)
                         .OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                yield return Path.Combine(directory, "codex.exe");
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = directory.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    yield return Path.Combine(trimmed, "codex.exe");
                }
            }
        }
    }

    private static string? FindUsingWhere()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "where.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("codex.exe");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(3_000) || process.ExitCode != 0)
            {
                return null;
            }

            return output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(File.Exists);
        }
        catch
        {
            return null;
        }
    }
}
