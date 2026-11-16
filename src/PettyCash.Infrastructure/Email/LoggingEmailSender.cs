using Microsoft.Extensions.Logging;
using PettyCash.Application.Abstractions;

namespace PettyCash.Infrastructure.Email;

/// <summary>Development fallback when SMTP is not configured.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var attachmentNote = message.Attachments is { Count: > 0 }
            ? $" (+ {message.Attachments.Count} attachment(s))"
            : string.Empty;

        logger.LogWarning(
            "Email was not sent because mail is not set up. To={To} Subject={Subject}{Attachments}",
            message.ToAddress,
            message.Subject,
            attachmentNote);

        return Task.CompletedTask;
    }
}
