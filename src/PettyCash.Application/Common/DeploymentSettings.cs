namespace PettyCash.Application.Common;

public static class DeploymentSettings
{
    public static bool IsAcceptableInitialPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 128)
        {
            return false;
        }

        if (password.Contains('\n') || password.Contains('\r'))
        {
            return false;
        }

        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        foreach (var character in password)
        {
            if (char.IsUpper(character)) hasUpper = true;
            else if (char.IsLower(character)) hasLower = true;
            else if (char.IsDigit(character)) hasDigit = true;
        }

        return hasUpper && hasLower && hasDigit;
    }

    public static bool RequiresEncryption(string connectionString)
    {
        return Contains(connectionString, "SSL Mode=Require")
            || Contains(connectionString, "Ssl Mode=Require")
            || Contains(connectionString, "SSL Mode=VerifyFull")
            || Contains(connectionString, "SSL Mode=VerifyCA")
            || Contains(connectionString, "sslmode=require")
            || Contains(connectionString, "sslmode=verify-full")
            || Contains(connectionString, "sslmode=verify-ca");
    }

    public static bool DisablesSsl(string connectionString)
    {
        return Contains(connectionString, "SSL Mode=Disable")
            || Contains(connectionString, "sslmode=disable");
    }

    private static bool Contains(string value, string fragment)
    {
        return value.Contains(fragment, StringComparison.OrdinalIgnoreCase);
    }
}
