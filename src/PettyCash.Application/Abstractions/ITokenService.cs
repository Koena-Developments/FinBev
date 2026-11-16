namespace PettyCash.Application.Abstractions;

public interface ITokenService
{
    string CreateToken(string userId, string username, IEnumerable<string> roles);
}
