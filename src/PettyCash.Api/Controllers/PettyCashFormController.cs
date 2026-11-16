using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Common;
using PettyCash.Application.Dtos;

namespace PettyCash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/petty-cash-form")]
public class PettyCashFormController(IPettyCashFormService formService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("{requestId:guid}")]
    public async Task<ActionResult<PettyCashFormDto>> Get(Guid requestId, CancellationToken cancellationToken)
    {
        var form = await formService.GetFormAsync(
            requestId,
            currentUser.UserId!,
            currentUser.IsInRole(AppRoles.Admin),
            cancellationToken);
        if (form is null)
        {
            var exists = await formService.RequestExistsAsync(requestId, cancellationToken);
            return exists
                ? StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to view this petty cash form." })
                : NotFound(new { message = "This petty cash request could not be found." });
        }

        return Ok(form);
    }

    [HttpGet("{requestId:guid}/document.docx")]
    public async Task<IActionResult> DownloadDocx(Guid requestId, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await formService.GenerateDocxAsync(
                requestId,
                currentUser.UserId!,
                currentUser.IsInRole(AppRoles.Admin),
                cancellationToken);
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"PettyCash-{requestId:N}.docx");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }

    [Authorize(Policy = "Approver")]
    [HttpGet("finance-copies")]
    public async Task<ActionResult<IReadOnlyList<PettyCashFormCopyItemDto>>> FinanceCopies(CancellationToken cancellationToken)
    {
        return Ok(await formService.ListFinanceCopiesAsync(currentUser.UserId!, cancellationToken));
    }
}
