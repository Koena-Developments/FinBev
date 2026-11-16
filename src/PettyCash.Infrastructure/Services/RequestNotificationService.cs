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
    IPettyCashFormService formService,
    IEmailSender emailSender,
    GraphMailboxTokenProvider mailboxTokens,
    IOptions<EmailOptions> emailOptions,
    IOptions<PettyCashFormOptions> formOptions,
    ILogger<RequestNotificationService> logger) : IRequestNotificationService
{
    public async Task NotifySubmittedAsync(Guid cashRequestId, string senderUserId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadContextAsync(cashRequestId, cancellationToken);
        if (ctx is null)
        {
            return;
        }

        var displayId = ToDisplayId(cashRequestId);
        var summary = FormatSummary(ctx);

        await SendToUserAsync(
            senderUserId,
            ctx.Claimant,
            $"Petty cash request submitted — {displayId}",
            Body(
                $"Hello {ctx.Claimant.DisplayName},",
                "Your petty cash request has been submitted and is awaiting your department senior manager's signature.",
                summary,
                FormLink(ctx.RequestId),
                ApprovalsLink()),
            cancellationToken);

        var deptApprover = ctx.PendingDepartmentTask?.AssignedApprover;
        if (deptApprover is not null)
        {
            await SendToUserAsync(
                senderUserId,
                deptApprover,
                $"Action required: sign petty cash form — {displayId}",
                Body(
                    $"Hello {deptApprover.DisplayName},",
                    $"{ctx.Claimant.DisplayName} submitted a petty cash request that requires your signature on the official form.",
                    summary,
                    FormLink(ctx.RequestId),
                    ApprovalsLink()),
                cancellationToken);
        }
    }

    public async Task NotifyDepartmentApprovedAsync(Guid cashRequestId, string senderUserId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadContextAsync(cashRequestId, cancellationToken);
        if (ctx is null)
        {
            return;
        }

        var displayId = ToDisplayId(cashRequestId);
        var summary = FormatSummary(ctx);
        byte[]? docx = null;

        try
        {
            docx = await formService.GenerateDocxForNotificationAsync(cashRequestId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not build Word form for email attachments on request {RequestId}", cashRequestId);
        }

        var attachment = docx is { Length: > 0 }
            ? new[] { new EmailAttachment($"PettyCash-{displayId}.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docx) }
            : null;

        await SendToUserAsync(
            senderUserId,
            ctx.Claimant,
            $"Department approved your petty cash request — {displayId}",
            Body(
                $"Hello {ctx.Claimant.DisplayName},",
                "Your department senior manager has signed the petty cash form. The request is now with Finance for approval.",
                summary,
                FormLink(ctx.RequestId)),
            cancellationToken);

        var financeApprover = ctx.PendingFinanceTask?.AssignedApprover;
        if (financeApprover is not null)
        {
            await SendToUserAsync(
                senderUserId,
                financeApprover,
                $"Finance approval required — petty cash {displayId}",
                Body(
                    $"Hello {financeApprover.DisplayName},",
                    "A petty cash request has been signed by the requester and department senior manager and requires Finance senior manager approval.",
                    summary,
                    FormLink(ctx.RequestId),
                    ApprovalsLink()),
                cancellationToken);
        }

        var copyUsernames = formOptions.Value.FinanceDocumentCopyUsernames ?? [];
        if (copyUsernames.Length > 0)
        {
            var copyUsers = await db.Users.AsNoTracking()
                .Where(u => copyUsernames.Contains(u.UserName!))
                .ToListAsync(cancellationToken);

            foreach (var copyUser in copyUsers)
            {
                await SendToUserAsync(
                    senderUserId,
                    copyUser,
                    $"Petty cash form copy — {displayId} (requester & department SM signed)",
                    Body(
                        $"Hello {copyUser.DisplayName},",
                        "Please find attached the petty cash form signed by the requester and department senior manager, released for Finance processing.",
                        summary,
                        FormLink(ctx.RequestId)),
                    cancellationToken,
                    attachment);
            }
        }
    }

    public async Task NotifyFinanceApprovedAsync(Guid cashRequestId, string senderUserId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadContextAsync(cashRequestId, cancellationToken);
        if (ctx is null)
        {
            return;
        }

        var displayId = ToDisplayId(cashRequestId);
        var summary = FormatSummary(ctx);

        await SendToUserAsync(
            senderUserId,
            ctx.Claimant,
            $"Finance approved your petty cash request — {displayId}",
            Body(
                $"Hello {ctx.Claimant.DisplayName},",
                "The Finance senior manager has approved your petty cash request. It is ready for cash disbursement by Finance.",
                summary,
                FormLink(ctx.RequestId)),
            cancellationToken);
    }

    public async Task NotifyRejectedAsync(Guid cashRequestId, string stageLabel, string senderUserId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadContextAsync(cashRequestId, cancellationToken);
        if (ctx is null)
        {
            return;
        }

        var displayId = ToDisplayId(cashRequestId);
        var summary = FormatSummary(ctx);

        await SendToUserAsync(
            senderUserId,
            ctx.Claimant,
            $"Petty cash request declined — {displayId}",
            Body(
                $"Hello {ctx.Claimant.DisplayName},",
                $"Your petty cash request was declined at the {stageLabel} step.",
                summary,
                RequestsLink()),
            cancellationToken);
    }

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

    private string RequestsLink() => $"{emailOptions.Value.PublicAppBaseUrl.TrimEnd('/')}/requests";

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
