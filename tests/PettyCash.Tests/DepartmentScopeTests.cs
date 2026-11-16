using Microsoft.EntityFrameworkCore;
using PettyCash.Domain.Entities;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Persistence;
using PettyCash.Infrastructure.Services;

namespace PettyCash.Tests;

public class DepartmentScopeTests
{
    [Fact]
    public async Task Department_manager_sees_only_the_department_they_manage()
    {
        await using var db = CreateDb();
        var own = new Department { Id = Guid.NewGuid(), Name = "Learning" };
        var other = new Department { Id = Guid.NewGuid(), Name = "Finance" };
        var manager = User(own.Id, "manager");
        db.Departments.AddRange(own, other);
        db.Users.Add(manager);
        db.ApprovalMatrices.Add(new ApprovalMatrix
        {
            Id = Guid.NewGuid(),
            DepartmentId = own.Id,
            PrimaryApproverUserId = manager.Id
        });
        await db.SaveChangesAsync();

        var departments = await new MasterDataService(db).GetDepartmentsAsync(manager.Id, canBrowseAll: false);

        Assert.Equal(["Learning"], departments.Select(d => d.Name).ToArray());
    }

    [Fact]
    public async Task Browsing_all_departments_is_not_limited_to_one()
    {
        await using var db = CreateDb();
        var own = new Department { Id = Guid.NewGuid(), Name = "Learning" };
        var other = new Department { Id = Guid.NewGuid(), Name = "Finance" };
        db.Departments.AddRange(own, other);
        await db.SaveChangesAsync();

        var departments = await new MasterDataService(db).GetDepartmentsAsync(null, canBrowseAll: true);

        Assert.Equal(2, departments.Count);
    }

    [Fact]
    public async Task History_lists_who_requested_in_the_managed_department_only()
    {
        await using var db = CreateDb();
        var own = new Department { Id = Guid.NewGuid(), Name = "Learning" };
        var other = new Department { Id = Guid.NewGuid(), Name = "Finance" };
        var manager = User(own.Id, "manager");
        var employee = User(own.Id, "lerato", "Lerato Dlamini");
        var outsider = User(other.Id, "finance-staff", "Finance Staff");
        db.Departments.AddRange(own, other);
        db.Users.AddRange(manager, employee, outsider);
        db.ApprovalMatrices.Add(new ApprovalMatrix
        {
            Id = Guid.NewGuid(),
            DepartmentId = own.Id,
            PrimaryApproverUserId = manager.Id
        });

        for (var i = 0; i < 7; i++)
        {
            db.CashRequests.Add(Request(own, employee, 100m + i, new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc).AddDays(i), $"Claim {i}"));
        }

        db.CashRequests.Add(Request(other, outsider, 900m, new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc), "Other department"));
        db.CashRequests.Add(new CashRequest
        {
            Id = Guid.NewGuid(),
            ClaimantUserId = employee.Id,
            DepartmentId = own.Id,
            BusinessUnitId = Guid.NewGuid(),
            BudgetCodeId = Guid.NewGuid(),
            ExpenseDate = new DateOnly(2026, 11, 3),
            Reason = "Still a draft",
            Amount = 50m,
            Status = RequestStatus.Draft,
            CreatedAtUtc = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var overview = await new ManagerDashboardService(db).GetOverviewAsync(manager.Id, 2026, 11);

        Assert.Equal(["Learning"], overview.ManagedDepartmentNames.ToArray());
        Assert.Equal(7, overview.RecentRequests.Count);
        Assert.All(overview.RecentRequests, row =>
        {
            Assert.Equal("Learning", row.DepartmentName);
            Assert.Equal("Lerato Dlamini", row.ClaimantName);
        });
        Assert.DoesNotContain(overview.RecentRequests, row => row.Reason == "Other department" || row.Reason == "Still a draft");
        Assert.Equal(100m + 101 + 102 + 103 + 104 + 105 + 106, overview.RecentRequests.Sum(row => row.Amount));
    }

    private static PettyCashDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PettyCashDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PettyCashDbContext(options);
    }

    private static AppUser User(Guid departmentId, string name, string? displayName = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = name,
        DisplayName = displayName ?? name,
        DepartmentId = departmentId,
        EntraUpn = $"{name}@test"
    };

    private static CashRequest Request(Department department, AppUser claimant, decimal amount, DateTime submittedAtUtc, string reason) => new()
    {
        Id = Guid.NewGuid(),
        ClaimantUserId = claimant.Id,
        DepartmentId = department.Id,
        BusinessUnitId = Guid.NewGuid(),
        BudgetCodeId = Guid.NewGuid(),
        ExpenseDate = DateOnly.FromDateTime(submittedAtUtc),
        Reason = reason,
        Amount = amount,
        Status = RequestStatus.Submitted,
        CreatedAtUtc = submittedAtUtc,
        SubmittedAtUtc = submittedAtUtc
    };
}
