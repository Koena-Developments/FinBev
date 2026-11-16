namespace PettyCash.Application.Abstractions;

public interface IMailboxConnectionLookup
{
    bool HasMailboxConnected(string userId);

    IReadOnlyList<string> ConnectedUserIds();
}
