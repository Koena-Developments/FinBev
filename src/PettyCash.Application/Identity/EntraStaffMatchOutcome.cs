namespace PettyCash.Application.Identity;

public enum EntraStaffMatchOutcome
{
    Matched,
    NotFound,
    Ambiguous
}

public sealed record EntraStaffMatchResult(EntraStaffMatchOutcome Outcome, string? UserId = null);