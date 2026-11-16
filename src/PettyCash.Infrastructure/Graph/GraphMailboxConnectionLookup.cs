using PettyCash.Application.Abstractions;

namespace PettyCash.Infrastructure.Graph;

public sealed class GraphMailboxConnectionLookup(GraphMailboxTokenProvider tokens) : IMailboxConnectionLookup
{
    public bool HasMailboxConnected(string userId) => tokens.HasMailboxConnected(userId);

    public IReadOnlyList<string> ConnectedUserIds() => tokens.ConnectedUserIds();
}
