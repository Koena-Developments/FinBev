using PettyCash.Application.Identity;

namespace PettyCash.Tests;

public class EntraStaffAccountMatcherTests
{
    private static EntraIdTokenPrincipal Principal(string oid, string? email = null, string? upn = null) =>
        new()
        {
            ObjectId = oid,
            TenantId = "11111111-1111-1111-1111-111111111111",
            Email = email,
            PreferredUsername = upn ?? email
        };

  [Fact]
  public void Matches_by_entra_object_id()
  {
    var staff = new List<EntraStaffAccountMatcher.StaffRow>
    {
      new("u1", "thabang.motswenyane", "thabangm@foodbev.co.za", null, "oid-1", "thabangm@foodbev.co.za"),
      new("u2", "other.user", "other@foodbev.co.za", null, null, "other@foodbev.co.za")
    };

    var match = EntraStaffAccountMatcher.Match(staff, Principal("oid-1", "thabangm@foodbev.co.za"));
    Assert.Equal(EntraStaffMatchOutcome.Matched, match.Outcome);
    Assert.Equal("u1", match.UserId);
  }

  [Fact]
  public void Matches_by_resolved_work_email_local_part()
  {
    var staff = new List<EntraStaffAccountMatcher.StaffRow>
    {
      new(
        "u1",
        "thabang.motswenyane",
        "thabangm@foodbev.co.za",
        null,
        null,
        "thabangm@foodbev.co.za")
    };

    var match = EntraStaffAccountMatcher.Match(staff, Principal("microsoft-oid", "thabangm@foodbev.co.za"));
    Assert.Equal(EntraStaffMatchOutcome.Matched, match.Outcome);
    Assert.Equal("u1", match.UserId);
  }

  [Fact]
  public void Rejects_unknown_accounts()
  {
    var staff = new List<EntraStaffAccountMatcher.StaffRow>
    {
      new("u1", "staff.one", "one@foodbev.co.za", null, null, "one@foodbev.co.za")
    };

    var match = EntraStaffAccountMatcher.Match(staff, Principal("unknown-oid", "stranger@foodbev.co.za"));
    Assert.Equal(EntraStaffMatchOutcome.NotFound, match.Outcome);
  }

  [Fact]
  public void Rejects_ambiguous_shared_email()
  {
    var staff = new List<EntraStaffAccountMatcher.StaffRow>
    {
      new("u1", "staff.one", "shared@foodbev.co.za", null, null, "shared@foodbev.co.za"),
      new("u2", "staff.two", "shared@foodbev.co.za", null, null, "shared@foodbev.co.za")
    };

    var match = EntraStaffAccountMatcher.Match(staff, Principal("oid-x", "shared@foodbev.co.za"));
    Assert.Equal(EntraStaffMatchOutcome.Ambiguous, match.Outcome);
  }

  [Fact]
  public void Matches_by_entra_upn()
  {
    var staff = new List<EntraStaffAccountMatcher.StaffRow>
    {
      new("u1", "staff.one", "alias@foodbev.co.za", "one@foodbev.co.za", null, "alias@foodbev.co.za")
    };

    var match = EntraStaffAccountMatcher.Match(staff, Principal("oid-1", "one@foodbev.co.za"));
    Assert.Equal(EntraStaffMatchOutcome.Matched, match.Outcome);
    Assert.Equal("u1", match.UserId);
  }
}