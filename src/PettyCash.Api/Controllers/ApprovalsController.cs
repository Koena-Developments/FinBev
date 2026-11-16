using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;

namespace PettyCash.Api.Controllers;

[ApiController]
[Authorize(Policy = "Approver")]
[Route("api/approvals")]
public class ApprovalsController(IApprovalService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("inbox")]
    public async Task<ActionResult<IReadOnlyList<ApprovalInboxItemDto>>> Inbox(CancellationToken cancellationToken)
    {
        return Ok(await service.GetInboxAsync(currentUser.UserId!, cancellationToken));
    }

    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<ApprovalQueueItemDto>>> Queue(CancellationToken cancellationToken)
    {
        return Ok(await service.GetQueueAsync(currentUser.UserId!, cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await service.ApproveAsync(id, currentUser.UserId!, request, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ClientErrors.ForException(ex) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await service.RejectAsync(id, currentUser.UserId!, request, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ClientErrors.ForException(ex) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }
}
