namespace PettyCash.Domain.Policy;

/// <summary>
/// After the receipt window closes, send an overdue reminder twice, one day apart, then stop.
/// </summary>
public static class ReceiptReminderSchedule
{
    public const int MaxOverdueSends = 2;

    public static TimeSpan Deadline => TimeSpan.FromDays(PettyCashPolicy.ReceiptDueDays);

    public static readonly TimeSpan OverdueRepeat = TimeSpan.FromDays(1);

    public static DateTime DueAt(DateTime disbursedAtUtc) => disbursedAtUtc + Deadline;

    public static bool IsOverdue(DateTime disbursedAtUtc, DateTime utcNow) =>
        utcNow >= DueAt(disbursedAtUtc);

    public static bool ShouldSend(DateTime disbursedAtUtc, DateTime utcNow, DateTime? lastSentAtUtc, int overdueSends)
    {
        if (utcNow < disbursedAtUtc || !IsOverdue(disbursedAtUtc, utcNow))
        {
            return false;
        }

        if (overdueSends >= MaxOverdueSends)
        {
            return false;
        }

        if (lastSentAtUtc is null)
        {
            return true;
        }

        return utcNow - lastSentAtUtc.Value >= OverdueRepeat;
    }
}
