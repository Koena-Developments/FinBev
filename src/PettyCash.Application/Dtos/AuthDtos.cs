namespace PettyCash.Application.Dtos;

public sealed record LoginRequest(string? Username, string Password, string? Email = null);

public sealed record LoginResponse(
    bool Success,
    bool MustChangePassword,
    string? Token,
    string? Message);

public sealed record ChangePasswordRequest(string? Username, string CurrentPassword, string NewPassword, string? Email = null);

public sealed record ForgotPasswordRequest(string? Email);

public sealed record ForgotPasswordResponse(bool Accepted, string Message);

public sealed record ResetPasswordRequest(string? Email, string? Code, string NewPassword);

public sealed record MeResponse(
    string UserId,
    string Username,
    string DisplayName,
    string Title,
    Guid DepartmentId,
    string DepartmentName,
    IReadOnlyList<string> Roles,
    bool MustChangePassword,
    /// <summary>True when the API is configured for delegated Graph mail (per-user device-code).</summary>
    bool RequiresDelegatedMailbox,
    /// <summary>True when RequiresDelegatedMailbox is false, or this user has stored a refresh token.</summary>
    bool MailboxConnected,
    string? Emoji);

public sealed record SetEmojiRequest(string? Emoji);

public sealed record MicrosoftSessionRequest(string IdToken, string Nonce);
