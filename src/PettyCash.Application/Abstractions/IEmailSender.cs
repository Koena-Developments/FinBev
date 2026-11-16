namespace PettyCash.Application.Abstractions;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailMessage(
    string ToAddress,
    string ToDisplayName,
    string Subject,
    string HtmlBody,
    string PlainTextBody,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    /// <summary>When using delegated Microsoft Graph, send as this portal user (AspNetUsers Id).</summary>
    string? SenderUserId = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
