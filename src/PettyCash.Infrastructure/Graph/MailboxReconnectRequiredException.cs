namespace PettyCash.Infrastructure.Graph;

/// <summary>
/// The stored Microsoft 365 refresh token cannot be used. The mailbox has to be connected again.
/// </summary>
public sealed class MailboxReconnectRequiredException(string message) : InvalidOperationException(message);
