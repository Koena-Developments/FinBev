using PettyCash.Domain;
using PettyCash.Domain.Policy;

namespace PettyCash.Tests;

public class ReceiptReminderScheduleTests
{
    private static readonly DateTime PaidAt = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Sends_once_during_the_three_day_window()
    {
        var during = PaidAt.AddHours(1);

        Assert.True(ReceiptReminderSchedule.ShouldSend(PaidAt, during, null));
        Assert.False(ReceiptReminderSchedule.ShouldSend(during, PaidAt.AddDays(1), during));
        Assert.False(ReceiptReminderSchedule.IsOverdue(PaidAt, PaidAt.AddDays(PettyCashPolicy.ReceiptDueDays).AddMinutes(-1)));
    }

    [Fact]
    public void Sends_again_when_the_deadline_passes_and_then_once_a_day()
    {
        var due = ReceiptReminderSchedule.DueAt(PaidAt);
        var remindedDuringWindow = PaidAt.AddHours(1);

        Assert.True(ReceiptReminderSchedule.IsOverdue(PaidAt, due));
        Assert.True(ReceiptReminderSchedule.ShouldSend(PaidAt, due, remindedDuringWindow));
        Assert.False(ReceiptReminderSchedule.ShouldSend(PaidAt, due.AddHours(12), due));
        Assert.True(ReceiptReminderSchedule.ShouldSend(PaidAt, due.AddDays(1), due));
    }

    [Fact]
    public void Does_not_send_before_the_cash_is_paid_out()
    {
        Assert.False(ReceiptReminderSchedule.ShouldSend(PaidAt, PaidAt.AddMinutes(-1), null));
    }
}
