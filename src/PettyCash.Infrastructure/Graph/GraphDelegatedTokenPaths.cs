using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PettyCash.Infrastructure.Graph;

internal static class GraphDelegatedTokenPaths
{
    /// <summary>MSAL cache files per portal user: graph-tokens/{userId}.msal</summary>
    public static string TokenFolder(IConfiguration configuration, IHostEnvironment env)
    {
        var secretsDir = configuration["Secrets:Directory"]
            ?? Path.Combine(env.ContentRootPath, "..", "..", "secrets");
        if (!Path.IsPathRooted(secretsDir))
        {
            secretsDir = Path.GetFullPath(secretsDir);
        }

        var folder = Path.Combine(secretsDir, "graph-tokens");
        return folder;
    }

    public static string CacheFilePath(string tokenFolder, string userId)
    {
        var safe = string.Concat(userId.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        return Path.Combine(tokenFolder, safe + ".msal");
    }
}
