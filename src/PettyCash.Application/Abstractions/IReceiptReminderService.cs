namespace PettyCash.Application.Abstractions;

public interface IReceiptReminderService
{
    /// <summary>
    /// Emails people who have been paid petty cash and still have not uploaded a receipt,
    /// and emails their senior manager to ask the employee to submit it.
    /// </summary>
    Task SendDueRemindersAsync(CancellationToken cancellationToken = default);
}
