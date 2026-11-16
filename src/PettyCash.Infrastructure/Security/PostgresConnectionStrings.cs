using Npgsql;

namespace PettyCash.Infrastructure.Security;

public static class PostgresConnectionStrings
{
    public static string Normalize(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "";
        }

        var value = ToKeywordForm(connectionString.Trim());
        var builder = new NpgsqlConnectionStringBuilder(value);
        if (builder.SslMode == SslMode.Disable)
        {
            return builder.ConnectionString;
        }

        if (builder.SslMode is not (SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull))
        {
            builder.SslMode = SslMode.Require;
        }

        return builder.ConnectionString;
    }

    private static string ToKeywordForm(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("The database address is not a valid Postgres URL.");
        }

        var userInfo = Uri.UnescapeDataString(uri.UserInfo);
        var separator = userInfo.IndexOf(':');
        var username = separator >= 0 ? userInfo[..separator] : userInfo;
        var password = separator >= 0 ? userInfo[(separator + 1)..] : "";
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Username = username,
            Password = password,
            Database = uri.AbsolutePath.Trim('/'),
        };
        if (uri.Port > 0)
        {
            builder.Port = uri.Port;
        }

        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(equals >= 0 ? pair[..equals] : pair);
            var raw = equals >= 0 ? Uri.UnescapeDataString(pair[(equals + 1)..]) : "";
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = raw.ToLowerInvariant() switch
                {
                    "disable" => SslMode.Disable,
                    "allow" => SslMode.Allow,
                    "prefer" => SslMode.Prefer,
                    "require" => SslMode.Require,
                    "verify-ca" or "verifyca" => SslMode.VerifyCA,
                    "verify-full" or "verifyfull" => SslMode.VerifyFull,
                    _ => SslMode.Prefer,
                };
            }
        }

        return builder.ConnectionString;
    }
}
