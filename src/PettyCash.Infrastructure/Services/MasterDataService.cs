using Microsoft.EntityFrameworkCore;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class MasterDataService(PettyCashDbContext db) : IMasterDataService
{
    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(
        string? userId,
        bool canBrowseAll,
        CancellationToken cancellationToken = default)
    {
        var query = db.Departments.AsNoTracking().AsQueryable();
        if (!canBrowseAll)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return [];
            }

            var managedIds = await db.ApprovalMatrices.AsNoTracking()
                .Where(m => m.PrimaryApproverUserId == userId || m.ActingApproverUserId == userId)
                .Select(m => m.DepartmentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (managedIds.Count == 0)
            {
                var departmentId = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => (Guid?)u.DepartmentId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (departmentId is null || departmentId == Guid.Empty)
                {
                    return [];
                }

                managedIds.Add(departmentId.Value);
            }

            query = query.Where(d => managedIds.Contains(d.Id));
        }

        return await query
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessUnitDto>> GetBusinessUnitsAsync(Guid? departmentId, CancellationToken cancellationToken = default)
    {
        var query = db.BusinessUnits.AsNoTracking().AsQueryable();
        if (departmentId.HasValue)
        {
            query = query.Where(b => b.DepartmentId == departmentId);
        }

        return await query.OrderBy(b => b.Name)
            .Select(b => new BusinessUnitDto(b.Id, b.Name, b.DepartmentId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetCodeDto>> GetBudgetCodesAsync(Guid? businessUnitId, CancellationToken cancellationToken = default)
    {
        var query = db.BudgetCodes.AsNoTracking().AsQueryable();
        if (businessUnitId.HasValue)
        {
            query = query.Where(c => c.BusinessUnitId == businessUnitId);
        }

        return await query.OrderBy(c => c.Code)
            .Select(c => new BudgetCodeDto(c.Id, c.Code, c.Description, c.BusinessUnitId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalMatrixDto>> GetApprovalMatricesAsync(CancellationToken cancellationToken = default)
    {
        return await db.ApprovalMatrices.AsNoTracking()
            .Include(m => m.Department)
            .Include(m => m.PrimaryApprover)
            .Include(m => m.ActingApprover)
            .OrderBy(m => m.Department.Name)
            .Select(m => new ApprovalMatrixDto(
                m.Id,
                m.DepartmentId,
                m.Department.Name,
                m.PrimaryApproverUserId,
                m.PrimaryApprover.DisplayName,
                m.ActingApproverUserId,
                m.ActingApprover != null ? m.ActingApprover.DisplayName : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<ApprovalMatrixDto> UpdateApprovalMatrixAsync(Guid departmentId, UpdateApprovalMatrixDto dto, CancellationToken cancellationToken = default)
    {
        var matrix = await db.ApprovalMatrices
            .Include(m => m.Department)
            .Include(m => m.PrimaryApprover)
            .Include(m => m.ActingApprover)
            .FirstOrDefaultAsync(m => m.DepartmentId == departmentId, cancellationToken)
            ?? throw new InvalidOperationException("No approvers are set up for that department.");

        matrix.PrimaryApproverUserId = dto.PrimaryApproverUserId;
        matrix.ActingApproverUserId = dto.ActingApproverUserId;
        await db.SaveChangesAsync(cancellationToken);

        matrix = await db.ApprovalMatrices.AsNoTracking()
            .Include(m => m.Department)
            .Include(m => m.PrimaryApprover)
            .Include(m => m.ActingApprover)
            .FirstAsync(m => m.Id == matrix.Id, cancellationToken);

        return new ApprovalMatrixDto(
            matrix.Id,
            matrix.DepartmentId,
            matrix.Department.Name,
            matrix.PrimaryApproverUserId,
            matrix.PrimaryApprover.DisplayName,
            matrix.ActingApproverUserId,
            matrix.ActingApprover?.DisplayName);
    }
}
