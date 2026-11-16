using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PettyCash.Api.Security;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;

namespace PettyCash.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, ICurrentUser currentUser, IHostEnvironment env) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return Ok(IssueSession(result));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("change-password")]
    public async Task<ActionResult<LoginResponse>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ChangePasswordAsync(request, cancellationToken);
        return Ok(IssueSession(result));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<ActionResult<LoginResponse>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ResetPasswordAsync(request, cancellationToken);
        return Ok(IssueSession(result));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthCookies.Name, AuthCookies.Issue(Request, env.IsDevelopment()));
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return Unauthorized();
        }

        var me = await authService.GetMeAsync(currentUser.UserId, cancellationToken);
        return me is null ? NotFound() : Ok(me);
    }

    [Authorize]
    [HttpPut("me/emoji")]
    public async Task<ActionResult<MeResponse>> SetEmoji([FromBody] SetEmojiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return Unauthorized(new { message = "Your session has ended. Sign in again." });
        }

        try
        {
            var me = await authService.SetEmojiAsync(currentUser.UserId, request.Emoji, cancellationToken);
            return me is null ? NotFound() : Ok(me);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ClientErrors.ForException(ex) });
        }
    }

    private LoginResponse IssueSession(LoginResponse result)
    {
        if (!string.IsNullOrEmpty(result.Token))
        {
            Response.Cookies.Append(AuthCookies.Name, result.Token, AuthCookies.Issue(Request, env.IsDevelopment()));
        }

        return result with { Token = null };
    }
}
