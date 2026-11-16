using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Options;

namespace PettyCash.Infrastructure.Email;

/// <summary>Writes .eml files when real SMTP/Graph is not configured (open in Outlook to preview).</summary>
public sealed class OutboxEmailSender(IOptions<EmailOptions> options, ILogger<OutboxEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var opt = options.Value;
        var folder = ResolveOutboxFolder(opt.OutboxFolder);
        Directory.CreateDirectory(folder);

        var toAddress = string.IsNullOrWhiteSpace(opt.RedirectAllTo) ? message.ToAddress : opt.RedirectAllTo;
        var toName = string.IsNullOrWhiteSpace(opt.RedirectAllTo) ? message.ToDisplayName : $"{message.ToDisplayName} (redirected)";

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(opt.FromName, opt.FromAddress));
        mime.To.Add(new MailboxAddress(toName, toAddress));
        mime.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody
        };

        if (message.Attachments is not null)
        {
            foreach (var attachment in message.Attachments)
            {
                builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
        }

        mime.Body = builder.ToMessageBody();

        var safeSubject = string.Concat(message.Subject.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        if (safeSubject.Length > 60)
        {
            safeSubject = safeSubject[..60];
        }

        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeSubject}.eml";
        var path = Path.Combine(folder, fileName);
        await using var stream = File.Create(path);
        await mime.WriteToAsync(stream, cancellationToken);

        logger.LogWarning(
            "Email NOT sent to inbox (SMTP/Graph off). Saved to {Path}. To={To} Subject={Subject}",
            path,
            toAddress,
            message.Subject);
    }

    private static string ResolveOutboxFolder(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Path.Combine(AppContext.BaseDirectory, "email-outbox");
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
    }
}
