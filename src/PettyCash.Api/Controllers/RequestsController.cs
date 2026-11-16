using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Common;
using PettyCash.Application.Dtos;

namespace PettyCash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/requests")]
public class RequestsController(ICashRequestService service, ICurrentUser currentUser) : ControllerBase
{
    [Authorize(Policy = "Admin")]
    [HttpGet("pending-disbursement")]
    public async Task<ActionResult<IReadOnlyList<CashRequestSummaryDto>>> PendingDisbursement(CancellationToken cancellationToken)
    {
        return Ok(await service.ListPendingDisbursementAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CashRequestSummaryDto>>> ListMine(CancellationToken cancellationToken)
    {
        return Ok(await service.ListMineAsync(currentUser.UserId!, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CashRequestDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var detail = await service.GetAsync(
            id,
            currentUser.UserId!,
            currentUser.IsInRole(AppRoles.Admin),
            currentUser.IsInRole(AppRoles.Approver),
            cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost]
    public async Task<ActionResult<CashRequestDetailDto>> Create([FromBody] CreateCashRequestDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateDraftAsync(currentUser.UserId!, dto, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<CashRequestDetailDto>> Submit(
        Guid id,
        [FromBody] SubmitCashRequestDto submit,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.SubmitAsync(id, currentUser.UserId!, submit, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }

    [Authorize(Policy = "Admin")]
    [HttpPost("{id:guid}/disburse")]
    public async Task<IActionResult> Disburse(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await service.DisburseAsync(id, currentUser.UserId!, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await service.CancelAsync(id, currentUser.UserId!, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }
}
