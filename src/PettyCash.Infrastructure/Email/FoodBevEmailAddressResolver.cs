using PettyCash.Application.Options;
using PettyCash.Domain.Entities;

namespace PettyCash.Infrastructure.Email;

/// <summary>
/// FoodBev convention: {firstName}{firstLetterOfSurname}@foodbev.co.za (from portal username firstname.surname).
/// </summary>
public static class FoodBevEmailAddressResolver
{
    public static string? Resolve(AppUser user, EmailOptions options)
    {
        if (!string.IsNullOrWhiteSpace(user.EntraUpn) && user.EntraUpn.Contains('@', StringComparison.Ordinal))
        {
            return user.EntraUpn.Trim();
        }

        if (!string.IsNullOrWhiteSpace(user.Email)
            && !user.Email.EndsWith("@pettycash.local", StringComparison.OrdinalIgnoreCase))
        {
            return user.Email.Trim();
        }

        var username = user.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        if (options.AddressOverrides.TryGetValue(username, out var overrideAddress)
            && !string.IsNullOrWhiteSpace(overrideAddress))
        {
            return overrideAddress.Trim();
        }

        var domain = options.DefaultEmailDomain?.Trim();
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        var parts = username.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            var first = parts[0].ToLowerInvariant();
            var surnameInitial = parts[^1][0].ToString().ToLowerInvariant();
            return $"{first}{surnameInitial}@{domain}";
        }

        return $"{username.ToLowerInvariant()}@{domain}";
    }
}
