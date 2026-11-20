using PettyCash.Application.Options;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Email;

namespace PettyCash.Tests;

public class FoodBevApproverEmailTests
{
    private static readonly EmailOptions Options = new()
    {
        DefaultEmailDomain = "foodbev.co.za",
        AddressOverrides =
        {
            ["mpumelelo.qomiso"] = "MpumeleloQ@foodbev.co.za",
            ["goitseona.mmope"] = "GoitseonaM@foodbev.co.za"
        }
    };

    [Theory]
    [InlineData("mpumelelo.qomiso", "MpumeleloQ@foodbev.co.za")]
    [InlineData("goitseona.mmope", "GoitseonaM@foodbev.co.za")]
    public void Resolves_test_approver_mailboxes(string username, string expected)
    {
        var user = new AppUser { UserName = username, Email = $"{username}@pettycash.local" };
        var resolved = FoodBevEmailAddressResolver.Resolve(user, Options);
        Assert.Equal(expected, resolved);
    }
}