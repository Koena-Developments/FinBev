using PettyCash.Domain;
using PettyCash.Domain.Policy;

namespace PettyCash.Tests;

public class RequestAmountValidatorTests
{
    [Theory]
    [InlineData(2000)]
    [InlineData(1)]
    [InlineData(1999.99)]
    public void Allows_amounts_up_to_cap(decimal amount)
    {
        Assert.True(RequestAmountValidator.IsValid(amount, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(2000.01)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_invalid_amounts(decimal amount)
    {
        Assert.False(RequestAmountValidator.IsValid(amount, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Policy_constants_match_plan()
    {
        Assert.Equal(2000m, PettyCashPolicy.MaxRequestAmount);
        Assert.Equal(10000m, PettyCashPolicy.FloatCapacity);
    }
}
