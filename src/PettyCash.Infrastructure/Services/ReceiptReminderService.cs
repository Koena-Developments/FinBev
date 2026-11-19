using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Options;
using PettyCash.Domain;
using PettyCash.Domain.Entities;
using PettyCash.Domain.Enums;
using PettyCash.Domain.Policy;
using PettyCash.Infrastructure.Email;
using PettyCash.Infrastructure.Graph;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public sealed class ReceiptReminderService(
    PettyCashDbContext db,
    IUploadService uploads,
    IEmailSender emailSender,
    IMailboxConnectionLookup mailboxes,
    IOptions<EmailOptions> emailOptions,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<ReceiptReminderService> logger) : IReceiptReminderService
{
    public async Task SendDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var requests = await db.CashRequests.AsNoTracking()
            .Include(request => request.Department)
            .Include(request => request.ApprovalTasks)
            .Where(request => request.Status == RequestStatus.Disbursed)
            .ToListAsync(cancellationToken);

        if (requests.Count == 0)
        {
            return;
        }

        var requestIds = requests.Select(request => request.Id).ToList();
        var payouts = await db.FloatLedgerEntries.AsNoTracking()
            .Where(entry => entry.EntryType == FloatLedgerEntryType.Disbursement && entry.CashRequestId != null)
            .Where(entry => requestIds.Contains(entry.CashRequestId!.Value))
            .Select(entry => new { entry.CashRequestId, entry.CreatedAtUtc, entry.CreatedByUserId })
            .ToListAsync(cancellationToken);

        var payoutByRequest = payouts
            .GroupBy(entry => entry.CashRequestId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(entry => entry.CreatedAtUtc).First());

        var withReceipts = await uploads.ListCashRequestIdsWithUploadsAsync(cancellationToken);
        var departmentIds = requests.Select(request => request.DepartmentId).Distinct().ToList();
        var primaryByDepartment = await db.ApprovalMatrices.AsNoTracking()
            .Where(matrix => departmentIds.Contains(matrix.DepartmentId))
            .ToDictionaryAsync(matrix => matrix.DepartmentId, matrix => matrix.PrimaryApproverUserId, cancellationToken);

        var reminders = await db.ReceiptReminders
            .Where(reminder => requestIds.Contains(reminder.CashRequestId))
            .ToDictionaryAsync(reminder => reminder.CashRequestId, cancellationToken);

        var userIds = requests.Select(request => request.ClaimantUserId)
            .Concat(requests.Select(request => ResolveManagerUserId(request, primaryByDepartment)))
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();
        var people = await db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var connectedMailboxes = RequiresDelegatedSender
            ? mailboxes.ConnectedUserIds()
            : [];
        if (RequiresDelegatedSender && connectedMailboxes.Count == 0)
        {
            logger.LogWarning("Skipped receipt reminders because no Microsoft 365 mailbox is connected.");
            return;
        }

        var sent = 0;
        var usedFallbackSender = false;
        var deadSenders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var request in requests)
        {
            if (withReceipts.Contains(request.Id) || !payoutByRequest.TryGetValue(request.Id, out var payout))
            {
                continue;
            }

            if (!people.TryGetValue(request.ClaimantUserId, out var employee))
            {
                continue;
            }

            var managerId = ResolveManagerUserId(request, primaryByDepartment);
            people.TryGetValue(managerId ?? "", out var manager);
            if (manager is not null && string.Equals(manager.Id, employee.Id, StringComparison.Ordinal))
            {
                manager = null;
            }

            reminders.TryGetValue(request.Id, out var reminder);
            var dueAtForCount = ReceiptReminderSchedule.DueAt(payout.CreatedAtUtc);
            var employeeSends = OverdueSendsSoFar(reminder?.EmployeeOverdueSends ?? 0, reminder?.EmployeeRemindedAtUtc, dueAtForCount);
            var managerSends = OverdueSendsSoFar(reminder?.ManagerOverdueSends ?? 0, reminder?.ManagerRemindedAtUtc, dueAtForCount);
            var remindEmployee = ReceiptReminderSchedule.ShouldSend(
                payout.CreatedAtUtc, now, reminder?.EmployeeRemindedAtUtc, employeeSends);
            var remindManager = manager is not null
                && ReceiptReminderSchedule.ShouldSend(
                    payout.CreatedAtUtc, now, reminder?.ManagerRemindedAtUtc, managerSends);
            if (!remindEmployee && !remindManager)
            {
                continue;
            }

            var senderUserId = ResolveSenderUserId(payout.CreatedByUserId, connectedMailboxes, deadSenders);
            if (RequiresDelegatedSender && senderUserId is null)
            {
                continue;
            }

            if (RequiresDelegatedSender
                && !string.Equals(senderUserId, payout.CreatedByUserId, StringComparison.Ordinal)
                && !usedFallbackSender)
            {
                usedFallbackSender = true;
                logger.LogInformation(
                    "Receipt reminders are being sent from mailbox {SenderUserId} because the finance user who paid the cash out has not connected a mailbox.",
                    senderUserId);
            }

            var overdue = ReceiptReminderSchedule.IsOverdue(payout.CreatedAtUtc, now);
            var dueAt = ReceiptReminderSchedule.DueAt(payout.CreatedAtUtc);
            var changed = false;
            if (reminder is null)
            {
                reminder = new ReceiptReminder { CashRequestId = request.Id };
                db.ReceiptReminders.Add(reminder);
                reminders[request.Id] = reminder;
            }

            if (remindEmployee
                && await SendWithFallbackAsync(
                    payout.CreatedByUserId,
                    connectedMailboxes,
                    deadSenders,
                    employee,
                    EmployeeSubject(request, overdue),
                    EmployeeBody(employee, request, payout.CreatedAtUtc, dueAt, overdue),
                    cancellationToken))
            {
                reminder.EmployeeRemindedAtUtc = now;
                reminder.EmployeeOverdueSends = employeeSends + 1;
                changed = true;
                sent++;
            }

            if (remindManager
                && manager is not null
                && await SendWithFallbackAsync(
                    payout.CreatedByUserId,
                    connectedMailboxes,
                    deadSenders,
                    manager,
                    ManagerSubject(employee, request, overdue),
                    ManagerBody(manager, employee, request, payout.CreatedAtUtc, dueAt, overdue),
                    cancellationToken))
            {
                reminder.ManagerRemindedAtUtc = now;
                reminder.ManagerOverdueSends = managerSends + 1;
                changed = true;
                sent++;
            }

            if (changed)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (db.Entry(reminder).State == EntityState.Added)
            {
                db.Entry(reminder).State = EntityState.Detached;
                reminders.Remove(request.Id);
            }
        }

        if (sent > 0)
        {
            logger.LogInformation("Sent {Count} petty cash receipt reminder(s).", sent);
        }
    }

    private static int OverdueSendsSoFar(int stored, DateTime? lastSentAtUtc, DateTime dueAtUtc)
    {
        if (stored > 0)
        {
            return stored;
        }

        // Claims already chased after the deadline under the old daily schedule are finished.
        if (lastSentAtUtc is not null && lastSentAtUtc.Value >= dueAtUtc)
        {
            return ReceiptReminderSchedule.MaxOverdueSends;
        }

        return 0;
    }

    private static string? ResolveSenderUserId(
        string? disbursedByUserId,
        IReadOnlyList<string> connectedMailboxes,
        IReadOnlySet<string> deadSenders)
    {
        if (!string.IsNullOrEmpty(disbursedByUserId)
            && connectedMailboxes.Contains(disbursedByUserId, StringComparer.Ordinal)
            && !deadSenders.Contains(disbursedByUserId))
        {
            return disbursedByUserId;
        }

        return connectedMailboxes.FirstOrDefault(id => !deadSenders.Contains(id));
    }

    private async Task<bool> SendWithFallbackAsync(
        string? disbursedByUserId,
        IReadOnlyList<string> connectedMailboxes,
        HashSet<string> deadSenders,
        AppUser user,
        string subject,
        (string Html, string Text) body,
        CancellationToken cancellationToken)
    {
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var senderUserId = RequiresDelegatedSender
                ? ResolveSenderUserId(disbursedByUserId, connectedMailboxes, deadSenders)
                : null;
            if (RequiresDelegatedSender && senderUserId is null)
            {
                return false;
            }

            if (senderUserId is not null && !attempted.Add(senderUserId))
            {
                return false;
            }

            var result = await TrySendAsync(senderUserId, user, subject, body, cancellationToken).ConfigureAwait(false);
            if (result == SendResult.MailboxRejected)
            {
                if (senderUserId is null)
                {
                    return false;
                }

                deadSenders.Add(senderUserId);
                logger.LogWarning(
                    "Mailbox for user {SenderUserId} can no longer send. Later receipt reminders in this run will use another connected mailbox.",
                    senderUserId);
                continue;
            }

            return result != SendResult.Failed;
        }
    }

    private bool RequiresDelegatedSender =>
        emailOptions.Value.UseDelegatedGraph
        && emailOptions.Value.UseMicrosoftGraph
        && !string.IsNullOrWhiteSpace(configuration["Graph:TenantId"])
        && !string.IsNullOrWhiteSpace(configuration["Graph:ClientId"]);

    private static string? ResolveManagerUserId(CashRequest request, IReadOnlyDictionary<Guid, string> primaryByDepartment)
    {
        var signerId = request.ApprovalTasks
            .Where(task => task.Stage == ApprovalStage.Department && task.Decision == ApprovalDecision.Approved)
            .OrderByDescending(task => task.DecidedAtUtc)
            .Select(task => task.AssignedApproverUserId)
            .FirstOrDefault(id => !string.IsNullOrEmpty(id));

        if (!string.IsNullOrEmpty(signerId))
        {
            return signerId;
        }

        return primaryByDepartment.TryGetValue(request.DepartmentId, out var primaryId) ? primaryId : null;
    }

    private enum SendResult
    {
        Sent,
        Skipped,
        Failed,
        MailboxRejected
    }

    private async Task<SendResult> TrySendAsync(
        string? senderUserId,
        AppUser user,
        string subject,
        (string Html, string Text) body,
        CancellationToken cancellationToken)
    {
        var address = FoodBevEmailAddressResolver.Resolve(user, emailOptions.Value);
        if (address is null)
        {
            logger.LogWarning("No email address for {Username}; skipped receipt reminder \"{Subject}\"", user.UserName, subject);
            return SendResult.Skipped;
        }

        var (html, text) = body;
        var redirect = emailOptions.Value.RedirectAllTo?.Trim();
        if (!string.IsNullOrWhiteSpace(redirect)
            && !string.Equals(redirect, address, StringComparison.OrdinalIgnoreCase))
        {
            html += $"<p><em>(Pilot: intended recipient {WebUtility.HtmlEncode(user.DisplayName)} &lt;{WebUtility.HtmlEncode(address)}&gt;)</em></p>";
            text += $"{Environment.NewLine}{Environment.NewLine}(Pilot: intended recipient {user.DisplayName} <{address}>)";
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
                    SenderUserId: RequiresDelegatedSender ? senderUserId : null),
                cancellationToken);
            return SendResult.Sent;
        }
        catch (MailboxReconnectRequiredException ex)
        {
            logger.LogWarning(ex, "Failed to send receipt reminder \"{Subject}\" to {Address}", subject, address);
            return SendResult.MailboxRejected;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send receipt reminder \"{Subject}\" to {Address}", subject, address);
            return SendResult.Failed;
        }
    }

    private static string EmployeeSubject(CashRequest request, bool overdue) =>
        overdue
            ? $"Overdue: upload your petty cash receipt — {DisplayId(request.Id)}"
            : $"Reminder: upload your petty cash receipt within {PettyCashPolicy.ReceiptDueDays} days — {DisplayId(request.Id)}";

    private static string ManagerSubject(AppUser employee, CashRequest request, bool overdue) =>
        overdue
            ? $"Overdue: please ask {employee.DisplayName} to submit their petty cash receipt — {DisplayId(request.Id)}"
            : $"Reminder: please ask {employee.DisplayName} to submit their petty cash receipt — {DisplayId(request.Id)}";

    private (string Html, string Text) EmployeeBody(AppUser employee, CashRequest request, DateTime paidAtUtc, DateTime dueAtUtc, bool overdue)
    {
        var lead = overdue
            ? $"You received petty cash on {FormatDate(paidAtUtc)}. The receipt was due within {PettyCashPolicy.ReceiptDueDays} days ({FormatDate(dueAtUtc)}) and is still outstanding. Please upload it now."
            : $"You received petty cash on {FormatDate(paidAtUtc)}. Please upload the invoice or receipt by {FormatDate(dueAtUtc)} (within {PettyCashPolicy.ReceiptDueDays} days).";
        return Body(
            $"Hello {employee.DisplayName},",
            lead,
            Summary(request, employee, paidAtUtc, dueAtUtc),
            UploadLink(request.Id));
    }

    private (string Html, string Text) ManagerBody(
        AppUser manager,
        AppUser employee,
        CashRequest request,
        DateTime paidAtUtc,
        DateTime dueAtUtc,
        bool overdue)
    {
        var lead = overdue
            ? $"{employee.DisplayName} received petty cash on {FormatDate(paidAtUtc)} and has not uploaded a receipt. It was due on {FormatDate(dueAtUtc)}. Please ask them to submit it."
            : $"{employee.DisplayName} received petty cash on {FormatDate(paidAtUtc)} and has not uploaded a receipt. Please ask them to submit it by {FormatDate(dueAtUtc)} (within {PettyCashPolicy.ReceiptDueDays} days).";
        return Body(
            $"Hello {manager.DisplayName},",
            lead,
            Summary(request, employee, paidAtUtc, dueAtUtc),
            RequestLink(request.Id));
    }

    private static (string Html, string Text) Body(string greeting, string lead, IReadOnlyList<string> summary, string link)
    {
        var summaryHtml = string.Join("<br/>", summary.Select(WebUtility.HtmlEncode));
        var html = new StringBuilder()
            .Append("<p>").Append(WebUtility.HtmlEncode(greeting)).Append("</p>")
            .Append("<p>").Append(WebUtility.HtmlEncode(lead)).Append("</p>")
            .Append("<p>").Append(summaryHtml).Append("</p>")
            .Append("<p><a href=\"").Append(link).Append("\">").Append(link).Append("</a></p>")
            .ToString();

        var text = new StringBuilder()
            .AppendLine(greeting)
            .AppendLine()
            .AppendLine(lead)
            .AppendLine()
            .AppendLine(string.Join(Environment.NewLine, summary))
            .AppendLine()
            .AppendLine(link)
            .ToString();

        return (html, text);
    }

    private static IReadOnlyList<string> Summary(CashRequest request, AppUser employee, DateTime paidAtUtc, DateTime dueAtUtc)
    {
        var amount = request.Amount.ToString("C", CultureInfo.GetCultureInfo("en-ZA"));
        return
        [
            $"Reference: {DisplayId(request.Id)}",
            $"Employee: {employee.DisplayName}",
            $"Department: {request.Department.Name}",
            $"Reason: {request.Reason}",
            $"Amount: {amount}",
            $"Paid out: {FormatDate(paidAtUtc)}",
            $"Receipt due: {FormatDate(dueAtUtc)}"
        ];
    }

    private string UploadLink(Guid requestId) =>
        $"{emailOptions.Value.PublicAppBaseUrl.TrimEnd('/')}/upload?claim={requestId:D}";

    private string RequestLink(Guid requestId) =>
        $"{emailOptions.Value.PublicAppBaseUrl.TrimEnd('/')}/requests/{requestId:D}";

    private static string DisplayId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private static string FormatDate(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SouthAfrica);
        return local.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-ZA"));
    }

    private static readonly TimeZoneInfo SouthAfrica = ResolveSouthAfrica();

    private static TimeZoneInfo ResolveSouthAfrica()
    {
        foreach (var id in new[] { "South Africa Standard Time", "Africa/Johannesburg" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            {
                return zone;
            }
        }

        return TimeZoneInfo.Utc;
    }
}
