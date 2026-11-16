using Microsoft.EntityFrameworkCore;
using PettyCash.Application.Abstractions;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Persistence;
using PettyCash.Infrastructure.Services;

namespace PettyCash.Tests;

public class ApprovalRoutingServiceTests
{
    [Fact]
    public async Task Routes_to_primary_when_not_on_leave()
    {
        await using var db = CreateDb();
        var (matrix, primaryId, actingId) = await SeedMatrixAsync(db, primaryOnLeave: false, actingOnLeave: false);
        var service = new ApprovalRoutingService(db, new FakeLeaveClient());

        var routed = await service.ResolveAsync(matrix);

        Assert.Equal(primaryId, routed.UserId);
        Assert.Null(routed.OnBehalfOfUserId);
    }

    [Fact]
    public async Task Routes_to_acting_when_primary_on_leave()
    {
        await using var db = CreateDb();
        var (matrix, primaryId, actingId) = await SeedMatrixAsync(db, primaryOnLeave: true, actingOnLeave: false);
        var service = new ApprovalRoutingService(db, new FakeLeaveClient());

        var routed = await service.ResolveAsync(matrix);

        Assert.Equal(actingId, routed.UserId);
        Assert.Equal(primaryId, routed.OnBehalfOfUserId);
        Assert.False(routed.DelegateUnavailable);
    }

    [Fact]
    public async Task Routes_to_primary_when_leave_cannot_be_checked()
    {
        await using var db = CreateDb();
        var (matrix, primaryId, _) = await SeedMatrixAsync(db, primaryOnLeave: true, actingOnLeave: false, cacheMinutesAgo: 60);
        var service = new ApprovalRoutingService(db, new UncheckedLeaveClient());

        var routed = await service.ResolveAsync(matrix);

        Assert.Equal(primaryId, routed.UserId);
        Assert.Null(routed.OnBehalfOfUserId);
    }

    [Fact]
    public async Task Flags_delegate_unavailable_when_both_on_leave()
    {
        await using var db = CreateDb();
        var (matrix, _, actingId) = await SeedMatrixAsync(db, primaryOnLeave: true, actingOnLeave: true);
        var service = new ApprovalRoutingService(db, new FakeLeaveClient());

        var routed = await service.ResolveAsync(matrix);

        Assert.Equal(actingId, routed.UserId);
        Assert.True(routed.DelegateUnavailable);
    }

    private static PettyCashDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PettyCashDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PettyCashDbContext(options);
    }

    private static async Task<(ApprovalMatrix matrix, string primaryId, string actingId)> SeedMatrixAsync(
        PettyCashDbContext db,
        bool primaryOnLeave,
        bool actingOnLeave,
        int cacheMinutesAgo = 0)
    {
        var dept = new Department { Id = Guid.NewGuid(), Name = "Finance" };
        db.Departments.Add(dept);

        var primary = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "primary",
            DisplayName = "Primary",
            DepartmentId = dept.Id,
            EntraUpn = "primary@test"
        };
        var acting = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "acting",
            DisplayName = "Acting",
            DepartmentId = dept.Id,
            EntraUpn = "acting@test"
        };
        db.Users.AddRange(primary, acting);

        db.ApproverAvailabilities.AddRange(
            new ApproverAvailability { UserId = primary.Id, IsOnLeave = primaryOnLeave, CheckedAtUtc = DateTime.UtcNow.AddMinutes(-cacheMinutesAgo), Source = "Test" },
            new ApproverAvailability { UserId = acting.Id, IsOnLeave = actingOnLeave, CheckedAtUtc = DateTime.UtcNow.AddMinutes(-cacheMinutesAgo), Source = "Test" });

        var matrix = new ApprovalMatrix
        {
            Id = Guid.NewGuid(),
            DepartmentId = dept.Id,
            PrimaryApproverUserId = primary.Id,
            ActingApproverUserId = acting.Id
        };
        db.ApprovalMatrices.Add(matrix);
        await db.SaveChangesAsync();
        return (matrix, primary.Id, acting.Id);
    }

    private sealed class FakeLeaveClient : ILeaveStatusClient
    {
        public Task<LeaveStatusResult> GetLeaveStatusAsync(string? entraUpn, CancellationToken cancellationToken = default)
            => Task.FromResult(new LeaveStatusResult(false, true, "Fake"));
    }

    private sealed class UncheckedLeaveClient : ILeaveStatusClient
    {
        public Task<LeaveStatusResult> GetLeaveStatusAsync(string? entraUpn, CancellationToken cancellationToken = default)
            => Task.FromResult(new LeaveStatusResult(false, false, "GraphError"));
    }
}
