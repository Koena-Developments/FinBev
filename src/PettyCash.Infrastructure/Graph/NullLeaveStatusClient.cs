using PettyCash.Application.Abstractions;

namespace PettyCash.Infrastructure.Graph;

public class NullLeaveStatusClient : ILeaveStatusClient
{
    public Task<LeaveStatusResult> GetLeaveStatusAsync(string? entraUpn, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entraUpn))
        {
            return Task.FromResult(new LeaveStatusResult(false, true, "NoUpn"));
        }

        return Task.FromResult(new LeaveStatusResult(false, true, "GraphNotConfigured"));
    }
}
