using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;
using PettyCash.Application.Options;
using PettyCash.Domain.Entities;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Persistence;
using PettyCash.Infrastructure.Services;

namespace PettyCash.Tests;

public class ReceiptReminderServiceTests
{
    private static readonly DateTime PaidAt = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Reminds_employee_and_senior_manager_until_a_receipt_is_uploaded()
    {
        await using var db = CreateDb();
        var claim = await SeedAsync(db);
        var mail = new CapturingEmailSender();
        var uploads = new ReceiptUploads();

        await SendAsync(db, mail, uploads, PaidAt.AddHours(1));

        Assert.Equal(2, mail.Messages.Count);
        var employee = mail.Messages.Single(message => message.ToAddress == "thandi@foodbev.co.za");
        var manager = mail.Messages.Single(message => message.ToAddress == "acting@foodbev.co.za");
        Assert.Contains("within 3 days", employee.Subject, StringComparison.Ordinal);
        Assert.Contains("please ask Thandi Nkosi", manager.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/upload?claim={claim.RequestId:D}", employee.PlainTextBody, StringComparison.Ordinal);
        Assert.Contains($"/requests/{claim.RequestId:D}", manager.PlainTextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(mail.Messages, message => message.ToAddress == "primary@foodbev.co.za");

        mail.Messages.Clear();
        await SendAsync(db, mail, uploads, PaidAt.AddDays(1));
        Assert.Empty(mail.Messages);

        await SendAsync(db, mail, uploads, PaidAt.AddDays(3));
        Assert.Equal(2, mail.Messages.Count);
        Assert.All(mail.Messages, message => Assert.StartsWith("Overdue:", message.Subject, StringComparison.Ordinal));

        mail.Messages.Clear();
        await SendAsync(db, mail, uploads, PaidAt.AddDays(3).AddHours(12));
        Assert.Empty(mail.Messages);

        await SendAsync(db, mail, uploads, PaidAt.AddDays(4));
        Assert.Equal(2, mail.Messages.Count);

        mail.Messages.Clear();
        uploads.WithReceipts.Add(claim.RequestId);
        await SendAsync(db, mail, uploads, PaidAt.AddDays(6));
        Assert.Empty(mail.Messages);
    }

    [Fact]
    public async Task Skips_claims_that_already_have_a_receipt()
    {
        await using var db = CreateDb();
        var claim = await SeedAsync(db);
        var mail = new CapturingEmailSender();
        var uploads = new ReceiptUploads();
        uploads.WithReceipts.Add(claim.RequestId);

        await SendAsync(db, mail, uploads, PaidAt.AddDays(5));

        Assert.Empty(mail.Messages);
        Assert.Empty(await db.ReceiptReminders.ToListAsync());
    }

    [Fact]
    public async Task Skips_cash_that_has_not_been_paid_out()
    {
        await using var db = CreateDb();
        await SeedAsync(db, RequestStatus.PendingDisbursement);
        var mail = new CapturingEmailSender();

        await SendAsync(db, mail, new ReceiptUploads(), PaidAt.AddDays(5));

        Assert.Empty(mail.Messages);
    }

    [Fact]
    public async Task Uses_the_department_senior_manager_when_nobody_has_signed()
    {
        await using var db = CreateDb();
        await SeedAsync(db, signedByActing: false);
        var mail = new CapturingEmailSender();

        await SendAsync(db, mail, new ReceiptUploads(), PaidAt.AddHours(2));

        Assert.Contains(mail.Messages, message => message.ToAddress == "primary@foodbev.co.za");
        Assert.DoesNotContain(mail.Messages, message => message.ToAddress == "acting@foodbev.co.za");
    }

    [Fact]
    public async Task Does_not_email_the_senior_manager_when_they_took_the_cash()
    {
        await using var db = CreateDb();
        await SeedAsync(db, employeeIsManager: true);
        var mail = new CapturingEmailSender();

        await SendAsync(db, mail, new ReceiptUploads(), PaidAt.AddHours(2));

        var message = Assert.Single(mail.Messages);
        Assert.Equal("thandi@foodbev.co.za", message.ToAddress);
    }

    [Fact]
    public async Task Retries_when_the_sender_mailbox_is_not_connected()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var mail = new CapturingEmailSender();
        var mailbox = new ConnectedMailbox();

        await SendAsync(db, mail, new ReceiptUploads(), PaidAt.AddHours(1), delegated: true, mailbox: mailbox);
        Assert.Empty(mail.Messages);
        Assert.Empty(await db.ReceiptReminders.ToListAsync());

        mailbox.Ids.Add("finance-user");
        await SendAsync(db, mail, new ReceiptUploads(), PaidAt.AddHours(2), delegated: true, mailbox: mailbox);
        Assert.Equal(2, mail.Messages.Count);
        Assert.All(mail.Messages, message => Assert.Equal("finance-user", message.SenderUserId));
    }

    [Fact]
    public async Task Retries_a_recipient_whose_email_failed()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var mail = new FlakyEmailSender();

        await SendAsync(db, mail, new ReceiptUploads(), PaidAt.AddHours(1));
        Assert.Single(mail.Messages);
        Assert.Equal("acting@foodbev.co.za", mail.Messages[0].ToAddress);

        await SendAsync(db, mail, new ReceiptUploads(), PaidAt.AddHours(2));
        Assert.Equal(2, mail.Messages.Count);
        Assert.Equal("thandi@foodbev.co.za", mail.Messages[1].ToAddress);
    }

    [Fact]
    public async Task Sends_from_another_connected_mailbox_when_finance_has_not_connected()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var mail = new CapturingEmailSender();

        await SendAsync(
            db,
            mail,
            new ReceiptUploads(),
            PaidAt.AddHours(1),
            delegated: true,
            mailbox: new ConnectedMailbox("someone-else"));

        Assert.Equal(2, mail.Messages.Count);
        Assert.All(mail.Messages, message => Assert.Equal("someone-else", message.SenderUserId));
    }

    private static async Task SendAsync(
        PettyCashDbContext db,
        IEmailSender mail,
        ReceiptUploads uploads,
        DateTime utcNow,
        bool delegated = false,
        ConnectedMailbox? mailbox = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(delegated
                ? new Dictionary<string, string?> { ["Graph:TenantId"] = "tenant", ["Graph:ClientId"] = "client" }
                : new Dictionary<string, string?>())
            .Build();
        var service = new ReceiptReminderService(
            db,
            uploads,
            mail,
            mailbox ?? new ConnectedMailbox("finance-user"),
            Options.Create(new EmailOptions
            {
                UseDelegatedGraph = delegated,
                UseMicrosoftGraph = delegated,
                PublicAppBaseUrl = "http://localhost:3000"
            }),
            configuration,
            new FixedTime(new DateTimeOffset(utcNow)),
            NullLogger<ReceiptReminderService>.Instance);
        await service.SendDueRemindersAsync();
    }

    private static PettyCashDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PettyCashDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PettyCashDbContext(options);
    }

    private static async Task<ClaimSeed> SeedAsync(
        PettyCashDbContext db,
        RequestStatus status = RequestStatus.Disbursed,
        bool signedByActing = true,
        bool employeeIsManager = false)
    {
        var department = new Department { Id = Guid.NewGuid(), Name = "Learning Programmes" };
        var employee = User("employee", "Thandi Nkosi", "thandi@foodbev.co.za", department.Id);
        var primary = User("primary", "Primary Senior Manager", "primary@foodbev.co.za", department.Id);
        var acting = User("acting", "Acting Senior Manager", "acting@foodbev.co.za", department.Id);
        var finance = User("finance-user", "Finance Cashier", "finance@foodbev.co.za", department.Id);
        db.Departments.Add(department);
        db.Users.AddRange(employee, primary, acting, finance);
        db.ApprovalMatrices.Add(new ApprovalMatrix
        {
            Id = Guid.NewGuid(),
            DepartmentId = department.Id,
            PrimaryApproverUserId = employeeIsManager ? employee.Id : primary.Id,
            ActingApproverUserId = acting.Id
        });

        var request = new CashRequest
        {
            Id = Guid.NewGuid(),
            ClaimantUserId = employee.Id,
            DepartmentId = department.Id,
            BusinessUnitId = Guid.NewGuid(),
            BudgetCodeId = Guid.NewGuid(),
            ExpenseDate = new DateOnly(2026, 11, 1),
            Reason = "Taxi fare",
            Amount = 250m,
            Status = status,
            CreatedAtUtc = PaidAt.AddDays(-2)
        };
        db.CashRequests.Add(request);

        if (signedByActing && !employeeIsManager)
        {
            db.ApprovalTasks.Add(new ApprovalTask
            {
                Id = Guid.NewGuid(),
                CashRequestId = request.Id,
                Stage = ApprovalStage.Department,
                AssignedApproverUserId = acting.Id,
                Decision = ApprovalDecision.Approved,
                DecidedAtUtc = PaidAt.AddDays(-1)
            });
        }

        if (status == RequestStatus.Disbursed)
        {
            db.FloatLedgerEntries.Add(new FloatLedgerEntry
            {
                Id = Guid.NewGuid(),
                FloatAccountId = Guid.NewGuid(),
                EntryType = FloatLedgerEntryType.Disbursement,
                Amount = request.Amount,
                CashRequestId = request.Id,
                CreatedByUserId = finance.Id,
                CreatedAtUtc = PaidAt,
                Note = "Petty cash disbursed"
            });
        }

        await db.SaveChangesAsync();
        return new ClaimSeed(request.Id);
    }

    private static AppUser User(string id, string name, string email, Guid departmentId) => new()
    {
        Id = id,
        UserName = id,
        DisplayName = name,
        Email = email,
        EntraUpn = email,
        DepartmentId = departmentId
    };

    private sealed record ClaimSeed(Guid RequestId);

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ConnectedMailbox(params string[] ids) : IMailboxConnectionLookup
    {
        public HashSet<string> Ids { get; } = [..ids];
        public bool HasMailboxConnected(string userId) => Ids.Contains(userId);
        public IReadOnlyList<string> ConnectedUserIds() => Ids.ToList();
    }

    private sealed class ReceiptUploads : IUploadService
    {
        public HashSet<Guid> WithReceipts { get; } = [];

        public Task<IReadOnlySet<Guid>> ListCashRequestIdsWithUploadsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(WithReceipts);

        public Task<IReadOnlyList<UploadedFileDto>> ListMineAsync(string userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UploadedFileDto>> ListForFinanceAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UploadedFileDto> SaveAsync(string userId, Guid cashRequestId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UploadedFileDto> AssignClaimAsync(string userId, Guid fileId, Guid cashRequestId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(UploadedFileDto Meta, Stream Content)?> OpenAsync(string userId, Guid id, bool asFinance, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(string userId, Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FlakyEmailSender : IEmailSender
    {
        private int calls;
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            calls++;
            if (calls == 1)
            {
                throw new InvalidOperationException("mailbox unavailable");
            }

            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
