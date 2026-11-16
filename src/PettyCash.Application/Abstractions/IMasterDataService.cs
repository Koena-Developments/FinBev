using PettyCash.Application.Dtos;

namespace PettyCash.Application.Abstractions;

public interface IMasterDataService
{
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(string? userId, bool canBrowseAll, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessUnitDto>> GetBusinessUnitsAsync(Guid? departmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BudgetCodeDto>> GetBudgetCodesAsync(Guid? businessUnitId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApprovalMatrixDto>> GetApprovalMatricesAsync(CancellationToken cancellationToken = default);
    Task<ApprovalMatrixDto> UpdateApprovalMatrixAsync(Guid departmentId, UpdateApprovalMatrixDto dto, CancellationToken cancellationToken = default);
}
