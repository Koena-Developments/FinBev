using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Common;
using PettyCash.Application.Dtos;
using PettyCash.Application.Options;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Graph;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class AuthService(
    UserManager<AppUser> userManager,
    PettyCashDbContext db,
    ITokenService tokenService,
    GraphMailboxTokenProvider graphMailboxTokens,
    IOptions<EmailOptions> emailOptions,
    IConfiguration configuration) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindByEmailAsync(request.Email, request.Username, cancellationToken);
        if (user is null)
        {
            return new LoginResponse(false, false, null, "Invalid email or password.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Locked();
        }

        var valid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!valid)
        {
            return await RejectPasswordAsync(user);
        }

        await ClearFailuresAsync(user);

        if (user.MustChangePassword)
        {
            return new LoginResponse(true, true, null, "Password change required.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.CreateToken(user.Id, user.UserName!, roles);
        return new LoginResponse(true, false, token, null);
    }

    public async Task<LoginResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindByEmailAsync(request.Email, request.Username, cancellationToken);
        if (user is null)
        {
            return new LoginResponse(false, false, null, "Invalid email or password.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Locked();
        }

        var valid = await userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!valid)
        {
            return await RejectPasswordAsync(user);
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return new LoginResponse(false, false, null, ClientErrors.ForIdentityCodes(result.Errors.Select(e => e.Code)));
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await ClearFailuresAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.CreateToken(user.Id, user.UserName!, roles);
        return new LoginResponse(true, false, token, null);
    }

    private async Task<AppUser?> FindByEmailAsync(string? email, string? username, CancellationToken cancellationToken)
    {
        var identifier = FirstSignIn(email, username);
        if (identifier is null || !identifier.Contains('@'))
        {
            return null;
        }

        var user = await userManager.FindByEmailAsync(identifier);
        if (user is not null)
        {
            return user;
        }

        var normalized = identifier.ToUpperInvariant();
        return await db.Users.FirstOrDefaultAsync(
            u => u.EntraUpn != null && u.EntraUpn.ToUpper() == normalized,
            cancellationToken);
    }

    private async Task<LoginResponse> RejectPasswordAsync(AppUser user)
    {
        if (!await userManager.GetLockoutEnabledAsync(user))
        {
            await userManager.SetLockoutEnabledAsync(user, true);
        }

        await userManager.AccessFailedAsync(user);
        if (await userManager.IsLockedOutAsync(user))
        {
            return Locked();
        }

        return new LoginResponse(false, false, null, "Invalid email or password.");
    }

    private async Task ClearFailuresAsync(AppUser user)
    {
        if (user.AccessFailedCount > 0)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }
    }

    private static LoginResponse Locked() =>
        new(false, false, null, "Too many sign-in attempts. Try again in a few minutes.");

    private static string? FirstSignIn(string? email, string? username)
    {
        foreach (var value in new[] { email, username })
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    public async Task<MeResponse?> GetMeAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var appUser = await userManager.FindByIdAsync(userId);
        var roles = appUser is null ? [] : await userManager.GetRolesAsync(appUser);

        var email = emailOptions.Value;
        var requiresDelegatedMailbox = email.UseDelegatedGraph
            && email.UseMicrosoftGraph
            && !string.IsNullOrWhiteSpace(configuration["Graph:TenantId"])
            && !string.IsNullOrWhiteSpace(configuration["Graph:ClientId"]);
        var mailboxConnected = !requiresDelegatedMailbox || graphMailboxTokens.HasMailboxConnected(user.Id);

        return new MeResponse(
            user.Id,
            user.UserName ?? "",
            user.DisplayName,
            user.Title,
            user.DepartmentId,
            user.Department.Name,
            roles.ToList(),
            user.MustChangePassword,
            requiresDelegatedMailbox,
            mailboxConnected,
            user.Emoji);
    }

    public async Task<MeResponse?> SetEmojiAsync(string userId, string? emoji, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(emoji))
        {
            user.Emoji = null;
        }
        else if (!StaffEmojiCatalog.TryNormalize(emoji, out var chosen))
        {
            throw new InvalidOperationException("Choose an emoji from the list.");
        }
        else
        {
            user.Emoji = chosen;
        }

        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new InvalidOperationException("We could not save your emoji. Try again.");
        }

        return await GetMeAsync(userId, cancellationToken);
    }
}
