using System.Diagnostics;
using System.Net.Sockets;

namespace PettyCash.Api;

internal static class DevPostgresBootstrap
{
    private const int DevPort = 5433;

    public static void EnsureStarted(ILogger logger, string? connectionString)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(connectionString)
            || !connectionString.Contains($"Port={DevPort}", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (IsPortOpen("127.0.0.1", DevPort))
        {
            return;
        }

        var scriptPath = FindInitScriptPath();
        if (scriptPath is null)
        {
            logger.LogWarning(
                "Dev PostgreSQL is not running on port {Port} and {Script} was not found. Run scripts/init-dev-postgres.ps1 from the repo root.",
                DevPort,
                "init-dev-postgres.ps1");
            return;
        }

        logger.LogInformation("Starting dev PostgreSQL (localhost:{Port})…", DevPort);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            WorkingDirectory = Path.GetDirectoryName(scriptPath) is { } scriptsDir
                ? Path.GetDirectoryName(scriptsDir) ?? scriptsDir
                : Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        process?.WaitForExit();
        if (process is null || process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to start dev PostgreSQL. Run manually: powershell -File \"{scriptPath}\"");
        }

        if (!IsPortOpen("127.0.0.1", DevPort))
        {
            throw new InvalidOperationException(
                $"Dev PostgreSQL did not become ready on localhost:{DevPort}. Check .postgres/server.log in the repo root.");
        }
    }

    private static string? FindInitScriptPath()
    {
        foreach (var root in EnumerateCandidateRoots())
        {
            var path = Path.Combine(root, "scripts", "init-dev-postgres.ps1");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = start;
            for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                if (seen.Add(dir))
                {
                    yield return dir;
                }

                dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
            }
        }
    }

    private static bool IsPortOpen(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            return task.Wait(TimeSpan.FromSeconds(2)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
