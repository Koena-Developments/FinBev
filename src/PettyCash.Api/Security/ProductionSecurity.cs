using System.Security.Cryptography;
using System.Text;

namespace PettyCash.Api.Security;

public static class ProductionSecurity
{
    public const string ProxyHeader = "X-PettyCash-Proxy";
    public const string ClientIpHeader = "X-PettyCash-Client";
    public const string FetchHeader = "X-Requested-With";
    public const string FetchHeaderValue = "PettyCash";
    public const string DevJwtKey = "DevOnlyKey_ChangeMe_32CharsMinimum!";

    public static string RequireJwtKey(IConfiguration configuration, IHostEnvironment env)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes. Set it with the Jwt__Key environment variable.");
        }

        if (!env.IsDevelopment() && string.Equals(key, DevJwtKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to start production with the development JWT signing key.");
        }

        return key;
    }

    public static void Validate(IConfiguration configuration, IHostEnvironment env)
    {
        RequireJwtKey(configuration, env);

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required. Set ConnectionStrings__DefaultConnection.");
        }

        if (env.IsDevelopment())
        {
            return;
        }

        if (connectionString.Contains("pettycash_dev", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to start production with the development database password.");
        }

        if (DeploymentSettings.DisablesSsl(connectionString) || !DeploymentSettings.RequiresEncryption(connectionString))
        {
            throw new InvalidOperationException("Production database connections must encrypt with SSL.");
        }

        if (!DeploymentSettings.IsAcceptableInitialPassword(configuration["Seed:InitialPassword"]))
        {
            throw new InvalidOperationException(
                "Set Seed__InitialPassword to a first password of at least 8 characters with an uppercase letter, a lowercase letter, and a number. New accounts use it once, then must change it. It is not written to a file.");
        }

        var secret = configuration["Proxy:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "Proxy:Secret must be at least 32 bytes in production. Set the same value as NUXT_PROXY_SECRET on Vercel.");
        }

        var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        if (origins.Length == 0
            || origins.Any(origin => string.IsNullOrWhiteSpace(origin)
                || !origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Cors:Origins must list the https Vercel origin in production.");
        }

        var publicApp = configuration["Email:PublicAppBaseUrl"];
        if (string.IsNullOrWhiteSpace(publicApp)
            || !publicApp.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Email:PublicAppBaseUrl must be the https Vercel URL in production.");
        }

        var secretsDirectory = configuration["Secrets:Directory"];
        if (string.IsNullOrWhiteSpace(secretsDirectory) || !Path.IsPathRooted(secretsDirectory))
        {
            throw new InvalidOperationException("Secrets:Directory must be an absolute path in production.");
        }
    }

    public static bool SecretMatches(string configured, string? presented)
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(presented ?? ""));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
