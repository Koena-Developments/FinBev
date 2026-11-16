using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;

namespace PettyCash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/float")]
public class FloatController(IFloatService service, ICurrentUser currentUser) : ControllerBase
{
    [Authorize(Policy = "Admin")]
    [HttpGet]
    public async Task<ActionResult<FloatSummaryDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await service.GetSummaryAsync(cancellationToken));
    }

    [Authorize(Policy = "Admin")]
    [HttpPost("replenishments")]
    public async Task<IActionResult> Replenish([FromBody] ReplenishmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await service.ReplenishAsync(currentUser.UserId!, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }
}
