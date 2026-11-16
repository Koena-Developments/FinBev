namespace PettyCash.Infrastructure.Services;

internal static class SignatureDecoder
{
    public static byte[]? Decode(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        var payload = base64.Trim();
        var comma = payload.IndexOf(',');
        if (comma >= 0)
        {
            payload = payload[(comma + 1)..];
        }

        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
