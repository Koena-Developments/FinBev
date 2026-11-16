namespace PettyCash.Application.Abstractions;

public interface ILeaveStatusClient
{
    Task<LeaveStatusResult> GetLeaveStatusAsync(string? entraUpn, CancellationToken cancellationToken = default);
}

public sealed record LeaveStatusResult(bool IsOnLeave, bool WasChecked, string Source);
