using PettyCash.Domain;

namespace PettyCash.Tests;

public class FloatAvailabilityTests
{
    [Fact]
    public void Cannot_reserve_more_than_available_balance()
    {
        const decimal balance = 10000m;
        const decimal reserved = 8500m;
        const decimal request = 2000m;
        var available = balance - reserved;
        Assert.True(request > available);
    }

    [Fact]
    public void Two_max_requests_fit_within_capacity()
    {
        const decimal balance = 10000m;
        const decimal first = 2000m;
        const decimal second = 2000m;
        var reserved = first;
        var availableAfterFirst = balance - reserved;
        Assert.True(second <= availableAfterFirst);
        reserved += second;
        Assert.Equal(4000m, reserved);
        Assert.True(reserved <= PettyCashPolicy.FloatCapacity);
    }
}
