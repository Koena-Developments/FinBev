using PettyCash.Application.Options;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Email;

namespace PettyCash.Tests;

public class FoodBevEmailAddressResolverTests
{
    [Fact]
    public void Resolves_firstname_plus_surname_initial()
    {
        var user = User("mpumelelo.qomiso", "Mpumelelo Qomiso");
        var email = FoodBevEmailAddressResolver.Resolve(user, Options());
        Assert.Equal("mpumeleloq@foodbev.co.za", email);
    }

    [Fact]
    public void Thabang_uses_override_not_thabangm()
    {
        var user = User("thabang.motswenyane", "Thabang Motswenyane");
        var email = FoodBevEmailAddressResolver.Resolve(user, Options());
        Assert.Equal("thabang@foodbev.co.za", email);
    }

    private static EmailOptions Options() => new()
    {
        DefaultEmailDomain = "foodbev.co.za",
        AddressOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["thabang.motswenyane"] = "thabang@foodbev.co.za",
            ["busisiwe.ntuli"] = "BusiN@foodbev.co.za",
            ["busisiwe.ngatsane"] = "Busisiwe@foodbev.co.za",
            ["tebogo.monare"] = "TebogoM@foodbev.co.za",
            ["tebogo.maaneke"] = "TebogoD@foodbev.co.za"
        }
    };

    [Theory]
    [InlineData("busisiwe.ntuli", "BusiN@foodbev.co.za")]
    [InlineData("busisiwe.ngatsane", "Busisiwe@foodbev.co.za")]
    [InlineData("tebogo.monare", "TebogoM@foodbev.co.za")]
    [InlineData("tebogo.maaneke", "TebogoD@foodbev.co.za")]
    public void Named_staff_use_their_real_address(string username, string expected)
    {
        var email = FoodBevEmailAddressResolver.Resolve(User(username, username), Options());
        Assert.Equal(expected, email);
    }

    private static AppUser User(string username, string displayName) => new()
    {
        UserName = username,
        DisplayName = displayName,
        Email = $"{username}@pettycash.local"
    };
}
