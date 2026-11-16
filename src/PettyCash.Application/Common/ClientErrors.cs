namespace PettyCash.Application.Common;

public static class ClientErrors
{
    public const string Unexpected = "Something went wrong on our side. Try again, and contact IT if it continues.";
    public const string InvalidDetails = "Some details are missing or not valid. Check the form and try again.";
    public const string PasswordRules = "Your new password must be at least 8 characters and include an uppercase letter, a lowercase letter, and a number.";

    public static string ForException(Exception exception)
    {
        if (exception is not (InvalidOperationException or ArgumentException or UnauthorizedAccessException))
        {
            return Unexpected;
        }

        var message = exception.Message.Trim();
        if (message.Length is 0 or > 240 || message.Contains('\n') || message.Contains('\r') || LooksTechnical(message))
        {
            return Unexpected;
        }

        return message;
    }

    public static string ForIdentityCodes(IEnumerable<string> codes)
    {
        foreach (var code in codes)
        {
            if (code is "PasswordTooShort" or "PasswordRequiresDigit" or "PasswordRequiresLower" or "PasswordRequiresUpper" or "PasswordRequiresNonAlphanumeric" or "PasswordRequiresUniqueChars")
            {
                return PasswordRules;
            }
        }

        return "We could not change your password. Check your current password and try again.";
    }

    private static bool LooksTechnical(string message)
    {
        string[] markers =
        [
            "\\",
            "SELECT ",
            "INSERT ",
            "UPDATE ",
            "DELETE ",
            "Exception",
            "password",
            "secret",
            "token",
            "Jwt:",
            ".cs:",
            " at "
        ];

        foreach (var marker in markers)
        {
            if (message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
