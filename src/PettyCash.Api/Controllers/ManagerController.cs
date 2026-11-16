using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Common;
using PettyCash.Application.Dtos;

namespace PettyCash.Api.Controllers;

[ApiController]
[Authorize(Policy = "Approver")]
[Route("api/manager")]
public class ManagerController(IManagerDashboardService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<ManagerOverviewDto>> Overview(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(AppRoles.Admin))
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        try
        {
            return Ok(await service.GetOverviewAsync(currentUser.UserId!, y, m, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }
}
