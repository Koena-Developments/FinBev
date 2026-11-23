using PettyCash.Application.Identity;

namespace PettyCash.Application.Abstractions;

public interface IEntraIdTokenValidator
{
    Task<EntraIdTokenPrincipal> ValidateAsync(string idToken, string nonce, CancellationToken cancellationToken = default);
}