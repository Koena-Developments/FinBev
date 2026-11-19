using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCash.Application.Abstractions;
using PettyCash.Infrastructure.Graph;

namespace PettyCash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/mailbox/microsoft")]
public class GraphMailboxController(GraphMailboxTokenProvider tokens, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<MailboxStatusDto> Status()
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return Unauthorized(new { message = "Your session has ended. Sign in again." });
        }

        var userId = currentUser.UserId!;
        return Ok(new MailboxStatusDto(tokens.HasMailboxConnected(userId)));
    }

    [HttpPost("device-code/start")]
    public async Task<ActionResult<DeviceCodeStartDto>> StartDeviceCode(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return Unauthorized(new { message = "Your session has ended. Sign in again." });
        }

        var userId = currentUser.UserId!;
        var result = await tokens.StartDeviceCodeAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new DeviceCodeStartDto(
            result.UserCode,
            result.VerificationUri,
            result.Message,
            result.ExpiresIn,
            result.IntervalSeconds));
    }

    [HttpPost("device-code/poll")]
    public async Task<ActionResult<DeviceCodePollDto>> PollDeviceCode(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return Unauthorized(new { message = "Your session has ended. Sign in again." });
        }

        var userId = currentUser.UserId!;
        var result = await tokens.PollDeviceCodeAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new DeviceCodePollDto(result.Status, result.Error));
    }
 

    public sealed record MailboxStatusDto(bool Connected);

    public sealed record DeviceCodeStartDto(
        string UserCode,
        string VerificationUri,
        string Message,
        int ExpiresIn,
        int IntervalSeconds);

    public sealed record DeviceCodePollDto(string Status, string? Error);
}
