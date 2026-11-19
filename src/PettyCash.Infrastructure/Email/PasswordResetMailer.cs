using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using AppSendMail = Microsoft.Graph.Users.Item.SendMail;
using MeSendMail = Microsoft.Graph.Me.SendMail;
using PettyCash.Application.Options;
using PettyCash.Infrastructure.Graph;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Email;

/// <summary>Sends the reset code from the connected Microsoft 365 mailbox, or from the app mailbox when no mailbox is connected.</summary>
public sealed class PasswordResetMailer(
    IConfiguration configuration,
    IOptions<EmailOptions> options,
    PettyCashDbContext db,
    GraphMailboxTokenProvider mailboxTokens,
    ILogger<PasswordResetMailer> logger)
{
    public async Task SendCodeAsync(string toAddress, string toDisplayName, string code, CancellationToken cancellationToken = default)
    {
        var senderUpn = options.Value.GraphSenderUpn?.Trim();
        if (string.IsNullOrWhiteSpace(senderUpn))
        {
            throw new InvalidOperationException("Email is not set up. Contact IT.");
        }

        var message = new Message
        {
            Subject = "Your FinBev code",
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = $"<p>Your FinBev code is <strong>{code}</strong>.</p><p>It expires in 15 minutes. If you did not ask for this, you can ignore this email.</p>"
            },
            ToRecipients =
            [
                new Recipient
                {
                    EmailAddress = new EmailAddress { Address = toAddress, Name = toDisplayName }
                }
            ]
        };

        var connectedUserId = await FindConnectedSenderAsync(senderUpn, cancellationToken).ConfigureAwait(false);
        if (connectedUserId is not null)
        {
            try
            {
                var graph = await mailboxTokens.CreateGraphClientAsync(connectedUserId, cancellationToken).ConfigureAwait(false);
                await graph.Me.SendMail.PostAsync(
                        new MeSendMail.SendMailPostRequestBody { Message = message, SaveToSentItems = true },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                logger.LogInformation("Sent a sign-in code to {Recipient} from the connected mailbox {Sender}", toAddress, senderUpn);
                return;
            }
            catch (MailboxReconnectRequiredException ex)
            {
                logger.LogWarning(ex, "Connected mailbox for {Sender} must be connected again", senderUpn);
                throw new InvalidOperationException("Connect the Microsoft 365 mailbox again under Account, then request a new code.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not email a sign-in code to {Recipient} from the connected mailbox", toAddress);
                throw new InvalidOperationException("We could not email the code. Try again in a moment.");
            }
        }

        var graphApp = GraphClientFactory.TryCreate(configuration);
        if (graphApp is null)
        {
            throw new InvalidOperationException("Email is not set up. Contact IT.");
        }

        try
        {
            await graphApp.Users[senderUpn].SendMail.PostAsync(
                    new AppSendMail.SendMailPostRequestBody { Message = message, SaveToSentItems = true },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not email a sign-in code to {Recipient}", toAddress);
            throw new InvalidOperationException("We could not email the code. Try again in a moment.");
        }

        logger.LogInformation("Sent a sign-in code to {Recipient}", toAddress);
    }

    private async Task<string?> FindConnectedSenderAsync(string senderUpn, CancellationToken cancellationToken)
    {
        var normalized = senderUpn.ToUpperInvariant();
        var userIds = await db.Users.AsNoTracking()
            .Where(user => user.NormalizedEmail == normalized
                || (user.EntraUpn != null && user.EntraUpn.ToUpper() == normalized))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return userIds.FirstOrDefault(mailboxTokens.HasMailboxConnected);
    }
}
