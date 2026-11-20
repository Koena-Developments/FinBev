using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Options;
using PettyCash.Domain.Entities;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Email;
using PettyCash.Infrastructure.Graph;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public sealed class RequestNotificationService(
    PettyCashDbContext db,
    IEmailSender emailSender,
    IMailboxConnectionLookup mailboxConnections,
    IOptions<EmailOptions> emailOptions,
    ILogger<RequestNotificationService> logger) : IRequestNotificationService
{
    public async Task NotifySubmittedAsync(Guid cashRequestId, string senderUserId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadContextAsync(cashRequestId, cancellationToken);
        if (ctx is null)
        {
            return;
        }

        var deptApprover = ctx.PendingDepartmentTask?.AssignedApprover;
        if (deptApprover is null)
        {
            return;
        }

        var displayId = ToDisplayId(cashRequestId);
        await SendToUserAsync(
            senderUserId,
            fallbackSenderUserId: senderUserId,
            deptApprover,
            $"Action required: sign petty cash form — {displayId}",
            Body(
                $"Hello {deptApprover.DisplayName},",
                $"{ctx.Claimant.DisplayName} submitted a petty cash request that requires your signature on the official form.",
                FormatSummary(ctx),
                FormLink(ctx.RequestId),
                ApprovalsLink()),
            cancellationToken);
    }

    public async Task NotifyDepartmentApprovedAsync(Guid cashRequestId, string senderUserId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadContextAsync(cashRequestId, cancellationToken);
        if (ctx is null)
        {
            return;
        }

        var financeApprover = ctx.PendingFinanceTask?.AssignedApprover;
        if (financeApprover is null)
        {
            return;
        }

        var displayId = ToDisplayId(cashRequestId);
        await SendToUserAsync(
            senderUserId,
            fallbackSenderUserId: ctx.Claimant.Id,
            financeApprover,
            $"Action required: Finance signature on petty cash form — {displayId}",
            Body(
                $"Hello {financeApprover.DisplayName},",
                $"The department senior manager has signed. This petty cash request now needs your signature as Finance senior manager.",
                FormatSummary(ctx),
                FormLink(ctx.RequestId),
                ApprovalsLink()),
            cancellationToken);
    }

    public Task NotifyFinanceApprovedAsync(Guid cashRequestId, string senderUserId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyRejectedAsync(Guid cashRequestId, string stageLabel, string senderUserId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private async Task SendToUserAsync(
        string senderUserId,
        string? fallbackSenderUserId,
        AppUser user,
        string subject,
        (string Html, string Text) body,
        CancellationToken cancellationToken,
        IReadOnlyList<EmailAttachment>? attachments = null)
    {
        var recipient = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken)
            ?? user;

        var address = ResolveEmailAddress(recipient);
        if (address is null)
        {
            logger.LogWarning("No email address for user {Username}; skipped \"{Subject}\"", recipient.UserName, subject);
            return;
        }

        var (html, text) = body;
        var redirect = emailOptions.Value.RedirectAllTo?.Trim();
        if (!string.IsNullOrWhiteSpace(redirect)
            && !string.Equals(redirect, address, StringComparison.OrdinalIgnoreCase))
        {
            var note = $"(Pilot: intended recipient {recipient.DisplayName} &lt;{address}&gt;)";
            var noteText = $"(Pilot: intended recipient {recipient.DisplayName} <{address}>)";
            html += $"<p><em>{note}</em></p>";
            text += $"{Environment.NewLine}{Environment.NewLine}{noteText}";
        }

        var delegated = emailOptions.Value.UseDelegatedGraph && emailOptions.Value.UseMicrosoftGraph;
        var resolvedSender = delegated
            ? ResolveDelegatedSender(senderUserId, fallbackSenderUserId)
            : senderUserId;

        if (delegated && resolvedSender is null)
        {
            logger.LogWarning(
                "Skipped email \"{Subject}\" to {Address} — no connected Microsoft 365 mailbox is available to send from.",
                subject,
                address);
            return;
        }

        if (delegated
            && !string.Equals(resolvedSender, senderUserId, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(senderUserId))
        {
            logger.LogInformation(
                "Email \"{Subject}\" to {Address} will send from mailbox {SenderUserId} because user {PreferredSenderUserId} has not connected Microsoft 365.",
                subject,
                address,
                resolvedSender,
                senderUserId);
        }

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    address,
                    recipient.DisplayName,
                    subject,
                    html,
                    text,
                    attachments,
                    SenderUserId: delegated ? resolvedSender : null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email \"{Subject}\" to {Address}", subject, address);
        }
    }

    private string? ResolveDelegatedSender(string preferredSenderUserId, string? fallbackSenderUserId)
    {
        var connected = mailboxConnections.ConnectedUserIds();
        if (connected.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(preferredSenderUserId)
            && connected.Contains(preferredSenderUserId, StringComparer.Ordinal))
        {
            return preferredSenderUserId;
        }

        if (!string.IsNullOrEmpty(fallbackSenderUserId)
            && connected.Contains(fallbackSenderUserId, StringComparer.Ordinal))
        {
            return fallbackSenderUserId;
        }

        return connected[0];
    }

    private string? ResolveEmailAddress(AppUser user) =>
        FoodBevEmailAddressResolver.Resolve(user, emailOptions.Value);

    private string FormLink(Guid requestId) =>
        $"{emailOptions.Value.PublicAppBaseUrl.TrimEnd('/')}/requests/{requestId:D}/form";

    private string ApprovalsLink() => $"{emailOptions.Value.PublicAppBaseUrl.TrimEnd('/')}/approvals";

    private static (string Html, string Text) Body(string greeting, string lead, string summary, params string[] links)
    {
        var linkHtml = string.Join("<br/>", links.Select(l => $"<a href=\"{l}\">{l}</a>"));
        var linkText = string.Join(Environment.NewLine, links);

        var html = new StringBuilder()
            .Append("<p>").Append(greeting).Append("</p>")
            .Append("<p>").Append(lead).Append("</p>")
            .Append("<p>").Append(summary.Replace("\n", "<br/>", StringComparison.Ordinal)).Append("</p>")
            .Append("<p>").Append(linkHtml).Append("</p>")
            .ToString();

        var text = new StringBuilder()
            .AppendLine(greeting)
            .AppendLine()
            .AppendLine(lead)
            .AppendLine()
            .AppendLine(summary)
            .AppendLine()
            .AppendLine(linkText)
            .ToString();

        return (html, text);
    }

    private static string FormatSummary(NotificationContext ctx)
    {
        var amount = ctx.Request.Amount.ToString("C", CultureInfo.GetCultureInfo("en-ZA"));
        return string.Join(
            "\n",
            $"Reference: {ToDisplayId(ctx.Request.Id)}",
            $"Requester: {ctx.Claimant.DisplayName}",
            $"Department: {ctx.Request.Department.Name}",
            $"Reason: {ctx.Request.Reason}",
            $"Amount: {amount}");
    }

    private static string ToDisplayId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private async Task<NotificationContext?> LoadContextAsync(Guid cashRequestId, CancellationToken cancellationToken)
    {
        var request = await db.CashRequests.AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.Claimant)
            .Include(r => r.ApprovalTasks)
            .ThenInclude(t => t.AssignedApprover)
            .FirstOrDefaultAsync(r => r.Id == cashRequestId, cancellationToken);

        if (request is null)
        {
            logger.LogWarning("Request {RequestId} not found for notification", cashRequestId);
            return null;
        }

        return new NotificationContext(
            request,
            request.Claimant,
            request.ApprovalTasks.FirstOrDefault(t => t.Stage == ApprovalStage.Department && t.Decision == ApprovalDecision.Pending),
            request.ApprovalTasks.FirstOrDefault(t => t.Stage == ApprovalStage.Finance && t.Decision == ApprovalDecision.Pending));
    }

    private sealed record NotificationContext(
        Domain.Entities.CashRequest Request,
        AppUser Claimant,
        Domain.Entities.ApprovalTask? PendingDepartmentTask,
        Domain.Entities.ApprovalTask? PendingFinanceTask)
    {
        public Guid RequestId => Request.Id;
    }
}
