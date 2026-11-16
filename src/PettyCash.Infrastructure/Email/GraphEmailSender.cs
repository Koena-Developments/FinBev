using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Options;
using PettyCash.Infrastructure.Graph;

namespace PettyCash.Infrastructure.Email;

/// <summary>Application-only Graph: send as GraphSenderUpn using client secret (requires Application Mail.Send).</summary>
public sealed class GraphEmailSender(
    IConfiguration configuration,
    IOptions<EmailOptions> options,
    ILogger<GraphEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var opt = options.Value;
        var senderUpn = opt.GraphSenderUpn?.Trim()
            ?? throw new InvalidOperationException("Email is not set up. Contact IT.");

        var graph = GraphClientFactory.TryCreate(configuration)
            ?? throw new InvalidOperationException("Email is not set up. Contact IT.");

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

        await graph.Users[senderUpn].SendMail.PostAsync(
            new SendMailPostRequestBody { Message = graphMessage, SaveToSentItems = true },
            cancellationToken: cancellationToken);

        logger.LogInformation("Sent email via Graph from {Sender} to {Recipient}: {Subject}", senderUpn, toAddress, message.Subject);
    }
}
