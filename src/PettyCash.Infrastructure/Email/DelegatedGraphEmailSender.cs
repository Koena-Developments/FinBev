using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using Microsoft.Graph.Me.SendMail;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Options;
using PettyCash.Infrastructure.Graph;

namespace PettyCash.Infrastructure.Email;

/// <summary>Sends mail as the portal user who connected their Microsoft 365 mailbox (delegated Mail.Send).</summary>
public sealed class DelegatedGraphEmailSender(
    GraphMailboxTokenProvider tokenProvider,
    IOptions<EmailOptions> options,
    ILogger<DelegatedGraphEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.SenderUserId))
        {
            throw new InvalidOperationException(
                "Connect your Microsoft 365 mailbox under Account, then Connect mailbox, before this email can be sent.");
        }

        var opt = options.Value;
        var toAddress = string.IsNullOrWhiteSpace(opt.RedirectAllTo) ? message.ToAddress : opt.RedirectAllTo;
        var toName = string.IsNullOrWhiteSpace(opt.RedirectAllTo) ? message.ToDisplayName : $"{message.ToDisplayName} (redirected)";

        var graphMessage = new Message
        {
            Subject = message.Subject,
            Body = new ItemBody { ContentType = BodyType.Html, Content = message.HtmlBody },
            ToRecipients =
            [
                new Recipient { EmailAddress = new EmailAddress { Address = toAddress, Name = toName } }
            ]
        };

        if (message.Attachments is { Count: > 0 })
        {
            graphMessage.Attachments = message.Attachments.Select(a => new FileAttachment
            {
                OdataType = "#microsoft.graph.fileAttachment",
                Name = a.FileName,
                ContentType = a.ContentType,
                ContentBytes = a.Content
            }).Cast<Attachment>().ToList();
        }

        var graph = await tokenProvider.CreateGraphClientAsync(message.SenderUserId!, cancellationToken).ConfigureAwait(false);
        await graph.Me.SendMail.PostAsync(
                new SendMailPostRequestBody { Message = graphMessage, SaveToSentItems = true },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Sent email via delegated Graph from user {SenderUserId} to {Recipient}: {Subject}",
            message.SenderUserId,
            toAddress,
            message.Subject);
    }
}
