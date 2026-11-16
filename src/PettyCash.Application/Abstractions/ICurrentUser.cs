namespace PettyCash.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
}
