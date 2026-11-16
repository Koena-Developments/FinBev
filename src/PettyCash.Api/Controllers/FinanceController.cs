using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;

namespace PettyCash.Api.Controllers;

[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/finance")]
public class FinanceController(IFinanceDashboardService service) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<FinanceOverviewDto>> Overview(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        try
        {
            return Ok(await service.GetOverviewAsync(y, m, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }
}
