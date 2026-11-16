using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Common;
using PettyCash.Application.Dtos;
using PettyCash.Infrastructure.Services;

namespace PettyCash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/uploads")]
public class UploadsController(IUploadService uploads, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UploadedFileDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await uploads.ListMineAsync(currentUser.UserId!, cancellationToken));
    }

    [Authorize(Policy = "Admin")]
    [HttpGet("finance")]
    public async Task<ActionResult<IReadOnlyList<UploadedFileDto>>> Finance(CancellationToken cancellationToken)
    {
        return Ok(await uploads.ListForFinanceAsync(cancellationToken));
    }

    [HttpPost]
    [RequestSizeLimit(UploadService.MaxBytes)]
    public async Task<ActionResult<UploadedFileDto>> Create(
        IFormFile? file,
        [FromForm] Guid? cashRequestId,
        CancellationToken cancellationToken)
    {
        if (cashRequestId is null || cashRequestId == Guid.Empty)
        {
            return BadRequest(new { message = "Choose the claim this document belongs to." });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a file to upload." });
        }

        if (file.Length > UploadService.MaxBytes)
        {
            return BadRequest(new { message = "Each file must be 10 MB or smaller." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var saved = await uploads.SaveAsync(
                currentUser.UserId!,
                cashRequestId.Value,
                stream,
                file.FileName,
                file.ContentType,
                cancellationToken);
            return Ok(saved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }

    [HttpPost("{id:guid}/claim")]
    public async Task<ActionResult<UploadedFileDto>> AssignClaim(
        Guid id,
        AssignUploadClaimRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CashRequestId == Guid.Empty)
        {
            return BadRequest(new { message = "Choose the claim this document belongs to." });
        }

        try
        {
            var saved = await uploads.AssignClaimAsync(currentUser.UserId!, id, request.CashRequestId, cancellationToken);
            return Ok(saved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var opened = await uploads.OpenAsync(
            currentUser.UserId!,
            id,
            currentUser.IsInRole(AppRoles.Admin),
            cancellationToken);
        if (opened is null)
        {
            return NotFound();
        }

        return File(
            opened.Value.Content,
            UploadService.DownloadContentType(opened.Value.Meta.FileName),
            opened.Value.Meta.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var removed = await uploads.DeleteAsync(currentUser.UserId!, id, cancellationToken);
        return removed ? NoContent() : NotFound();
    }
}
