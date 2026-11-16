namespace PettyCash.Domain.Policy;

/// <summary>
/// Remind once during the receipt window, again when the window closes, then once a day until a receipt is uploaded.
/// </summary>
public static class ReceiptReminderSchedule
{
    public static TimeSpan Deadline => TimeSpan.FromDays(PettyCashPolicy.ReceiptDueDays);

    public static readonly TimeSpan OverdueRepeat = TimeSpan.FromDays(1);

    public static DateTime DueAt(DateTime disbursedAtUtc) => disbursedAtUtc + Deadline;

    public static bool IsOverdue(DateTime disbursedAtUtc, DateTime utcNow) =>
        utcNow >= DueAt(disbursedAtUtc);

    public static bool ShouldSend(DateTime disbursedAtUtc, DateTime utcNow, DateTime? lastSentAtUtc)
    {
        if (utcNow < disbursedAtUtc)
        {
            return false;
        }

        if (lastSentAtUtc is null)
        {
            return true;
        }

        var dueAt = DueAt(disbursedAtUtc);
        if (utcNow < dueAt)
        {
            return false;
        }

        if (lastSentAtUtc.Value < dueAt)
        {
            return true;
        }

        return utcNow - lastSentAtUtc.Value >= OverdueRepeat;
    }
}
