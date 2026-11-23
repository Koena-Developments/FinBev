namespace PettyCash.Application.Identity;

/// <summary>Claims extracted from a validated Microsoft ID token.</summary>
public sealed class EntraIdTokenPrincipal
{
    public required string ObjectId { get; init; }
    public required string TenantId { get; init; }
    public string? Email { get; init; }
    public string? PreferredUsername { get; init; }

    public IEnumerable<string> SignInIdentities()
    {
        if (!string.IsNullOrWhiteSpace(PreferredUsername))
        {
            yield return PreferredUsername.Trim();
        }

        if (!string.IsNullOrWhiteSpace(Email))
        {
            var value = Email.Trim();
            if (!string.Equals(value, PreferredUsername, StringComparison.OrdinalIgnoreCase))
            {
                yield return value;
            }
        }
    }

    public IEnumerable<string> SignInIdentitiesNormalized()
    {
        return SignInIdentities()
            .Where(identity => identity.Contains('@', StringComparison.Ordinal))
            .Select(identity => identity.ToUpperInvariant())
            .Distinct();
    }
}