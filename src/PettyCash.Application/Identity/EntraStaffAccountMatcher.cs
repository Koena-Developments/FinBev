namespace PettyCash.Application.Identity;

/// <summary>
/// Maps a Microsoft account to an existing petty-cash staff row. Does not create accounts.
/// </summary>
public static class EntraStaffAccountMatcher
{
    public sealed record StaffRow(
        string UserId,
        string? UserName,
        string? Email,
        string? EntraUpn,
        string? EntraObjectId,
        string? ResolvedWorkEmail);

    public static EntraStaffMatchResult Match(
        IReadOnlyList<StaffRow> staff,
        EntraIdTokenPrincipal principal)
    {
        if (staff.Count == 0)
        {
            return new EntraStaffMatchResult(EntraStaffMatchOutcome.NotFound);
        }

        var oid = principal.ObjectId.Trim();
        var byOid = staff
            .Where(row => !string.IsNullOrWhiteSpace(row.EntraObjectId)
                && string.Equals(row.EntraObjectId, oid, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byOid.Count > 1)
        {
            return new EntraStaffMatchResult(EntraStaffMatchOutcome.Ambiguous);
        }

        if (byOid.Count == 1)
        {
            return new EntraStaffMatchResult(EntraStaffMatchOutcome.Matched, byOid[0].UserId);
        }

        var identities = principal.SignInIdentitiesNormalized().ToHashSet();
        if (identities.Count == 0)
        {
            return new EntraStaffMatchResult(EntraStaffMatchOutcome.NotFound);
        }

        var matched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in staff)
        {
            if (RowMatchesIdentities(row, identities))
            {
                matched.Add(row.UserId);
            }
        }

        if (matched.Count > 1)
        {
            return new EntraStaffMatchResult(EntraStaffMatchOutcome.Ambiguous);
        }

        if (matched.Count == 1)
        {
            return new EntraStaffMatchResult(EntraStaffMatchOutcome.Matched, matched.First());
        }

        return new EntraStaffMatchResult(EntraStaffMatchOutcome.NotFound);
    }

    private static bool RowMatchesIdentities(StaffRow row, HashSet<string> identities)
    {
        if (!string.IsNullOrWhiteSpace(row.EntraUpn)
            && identities.Contains(row.EntraUpn.Trim().ToUpperInvariant()))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(row.Email)
            && identities.Contains(row.Email.Trim().ToUpperInvariant()))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(row.ResolvedWorkEmail)
            && identities.Contains(row.ResolvedWorkEmail.Trim().ToUpperInvariant()))
        {
            return true;
        }

        foreach (var identity in identities)
        {
            var at = identity.IndexOf('@', StringComparison.Ordinal);
            if (at <= 0)
            {
                continue;
            }

            var local = identity[..at];
            var userName = row.UserName?.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                continue;
            }

            if (string.Equals(local, userName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}