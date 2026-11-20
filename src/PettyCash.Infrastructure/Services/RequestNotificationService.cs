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
    GraphMailboxTokenProvider mailboxTokens,
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
        AppUser user,
        string subject,
        (string Html, string Text) body,
        CancellationToken cancellationToken,
        IReadOnlyList<EmailAttachment>? attachments = null)
    {
        var address = ResolveEmailAddress(user);
        if (address is null)
        {
            logger.LogWarning("No email address for user {Username}; skipped \"{Subject}\"", user.UserName, subject);
            return;
        }

        var (html, text) = body;
        var redirect = emailOptions.Value.RedirectAllTo?.Trim();
        if (!string.IsNullOrWhiteSpace(redirect)
            && !string.Equals(redirect, address, StringComparison.OrdinalIgnoreCase))
        {
            var note = $"(Pilot: intended recipient {user.DisplayName} &lt;{address}&gt;)";
            var noteText = $"(Pilot: intended recipient {user.DisplayName} <{address}>)";
            html += $"<p><em>{note}</em></p>";
            text += $"{Environment.NewLine}{Environment.NewLine}{noteText}";
        }

        if (emailOptions.Value.UseDelegatedGraph
            && emailOptions.Value.UseMicrosoftGraph
            && !mailboxTokens.HasMailboxConnected(senderUserId))
        {
            logger.LogWarning(
                "Skipped email \"{Subject}\" — user {SenderUserId} has not connected their Microsoft 365 mailbox.",
                subject,
                senderUserId);
            return;
        }

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    address,
                    user.DisplayName,
                    subject,
                    html,
                    text,
                    attachments,
                    SenderUserId: emailOptions.Value.UseDelegatedGraph && emailOptions.Value.UseMicrosoftGraph
                        ? senderUserId
                        : null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email \"{Subject}\" to {Address}", subject, address);
        }
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
