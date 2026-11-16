using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Options;

namespace PettyCash.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var opt = options.Value;
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

        if (string.IsNullOrWhiteSpace(opt.SmtpHost))
        {
            throw new InvalidOperationException("Email could not be sent. Contact IT.");
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(opt.SmtpHost, opt.SmtpPort, opt.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);

        if (!string.IsNullOrWhiteSpace(opt.Username))
        {
            await client.AuthenticateAsync(opt.Username, opt.Password ?? "", cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Sent email \"{Subject}\" to {Recipient}", message.Subject, toAddress);
    }
}
