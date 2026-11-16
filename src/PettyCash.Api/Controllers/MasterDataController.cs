using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Common;
using PettyCash.Application.Dtos;

namespace PettyCash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class MasterDataController(IMasterDataService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> Departments(CancellationToken cancellationToken)
    {
        var canBrowseAll = currentUser.IsInRole(AppRoles.Admin);
        return Ok(await service.GetDepartmentsAsync(currentUser.UserId, canBrowseAll, cancellationToken));
    }

    [HttpGet("business-units")]
    public async Task<ActionResult<IReadOnlyList<BusinessUnitDto>>> BusinessUnits([FromQuery] Guid? departmentId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetBusinessUnitsAsync(departmentId, cancellationToken));
    }

    [HttpGet("budget-codes")]
    public async Task<ActionResult<IReadOnlyList<BudgetCodeDto>>> BudgetCodes([FromQuery] Guid? businessUnitId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetBudgetCodesAsync(businessUnitId, cancellationToken));
    }

    [Authorize(Policy = "Admin")]
    [HttpGet("approval-matrices")]
    public async Task<ActionResult<IReadOnlyList<ApprovalMatrixDto>>> ApprovalMatrices(CancellationToken cancellationToken)
    {
        return Ok(await service.GetApprovalMatricesAsync(cancellationToken));
    }

    [Authorize(Policy = "Admin")]
    [HttpPut("departments/{departmentId:guid}/approval-matrix")]
    public async Task<ActionResult<ApprovalMatrixDto>> UpdateMatrix(Guid departmentId, [FromBody] UpdateApprovalMatrixDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.UpdateApprovalMatrixAsync(departmentId, dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }
}
