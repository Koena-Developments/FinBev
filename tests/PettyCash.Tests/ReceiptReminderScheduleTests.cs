using PettyCash.Domain;
using PettyCash.Domain.Policy;

namespace PettyCash.Tests;

public class ReceiptReminderScheduleTests
{
    private static readonly DateTime PaidAt = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Does_not_send_before_the_receipt_is_overdue()
    {
        var during = PaidAt.AddHours(1);

        Assert.False(ReceiptReminderSchedule.ShouldSend(PaidAt, during, null, 0));
        Assert.False(ReceiptReminderSchedule.ShouldSend(PaidAt, PaidAt.AddDays(1), during, 0));
        Assert.False(ReceiptReminderSchedule.IsOverdue(PaidAt, PaidAt.AddDays(PettyCashPolicy.ReceiptDueDays).AddMinutes(-1)));
    }

    [Fact]
    public void Sends_twice_after_the_deadline_then_stops()
    {
        var due = ReceiptReminderSchedule.DueAt(PaidAt);

        Assert.True(ReceiptReminderSchedule.IsOverdue(PaidAt, due));
        Assert.True(ReceiptReminderSchedule.ShouldSend(PaidAt, due, null, 0));
        Assert.False(ReceiptReminderSchedule.ShouldSend(PaidAt, due.AddHours(12), due, 1));
        Assert.True(ReceiptReminderSchedule.ShouldSend(PaidAt, due.AddDays(1), due, 1));
        Assert.False(ReceiptReminderSchedule.ShouldSend(PaidAt, due.AddDays(2), due.AddDays(1), 2));
    }

    [Fact]
    public void Does_not_send_before_the_cash_is_paid_out()
    {
        Assert.False(ReceiptReminderSchedule.ShouldSend(PaidAt, PaidAt.AddMinutes(-1), null, 0));
    }
}
