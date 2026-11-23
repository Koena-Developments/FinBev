using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Common;
using PettyCash.Application.Dtos;
using PettyCash.Application.Identity;
using PettyCash.Application.Options;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Email;
using PettyCash.Infrastructure.Graph;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class AuthService(
    UserManager<AppUser> userManager,
    PettyCashDbContext db,
    ITokenService tokenService,
    GraphMailboxTokenProvider graphMailboxTokens,
    IOptions<EmailOptions> emailOptions,
    IConfiguration configuration,
    PasswordResetMailer resetMailer,
    IEntraIdTokenValidator entraIdTokenValidator) : IAuthService
{
    private const string MicrosoftSignInMessage = "Sign in with your FoodBev Microsoft account.";

    private bool MicrosoftSignInEnabled => MicrosoftSignInSettings.IsConfigured(configuration);

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (MicrosoftSignInEnabled)
        {
            return new LoginResponse(false, false, null, MicrosoftSignInMessage);
        }
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
        if (MicrosoftSignInEnabled)
        {
            return new LoginResponse(false, false, null, MicrosoftSignInMessage);
        }
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

    public async Task<ForgotPasswordResponse> SendResetCodeAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (MicrosoftSignInEnabled)
        {
            return new ForgotPasswordResponse(false, MicrosoftSignInMessage);
        }
        const string sent = "If that email belongs to a staff account, we sent a 4-digit code. It expires in 15 minutes.";
        var user = await FindByEmailAsync(request.Email, null, cancellationToken);
        if (user is null)
        {
            return new ForgotPasswordResponse(true, sent);
        }

        if (user.PasswordResetCodeAttempts >= 5
            && user.PasswordResetCodeExpiresUtc is DateTime lockedUntil
            && lockedUntil > DateTime.UtcNow)
        {
            return new ForgotPasswordResponse(false, "Too many tries. Wait 15 minutes, then request a new code.");
        }

        if (user.PasswordResetCodeExpiresUtc is DateTime alreadySent
            && alreadySent > DateTime.UtcNow.AddMinutes(14)
            && !string.IsNullOrWhiteSpace(user.PasswordResetCodeHash))
        {
            return new ForgotPasswordResponse(true, sent);
        }

        var attempts = user.PasswordResetCodeExpiresUtc > DateTime.UtcNow
            ? user.PasswordResetCodeAttempts
            : 0;
        var code = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
        user.PasswordResetCodeHash = HashResetCode(user.Id, code);
        user.PasswordResetCodeExpiresUtc = DateTime.UtcNow.AddMinutes(15);
        user.PasswordResetCodeAttempts = attempts;
        await userManager.UpdateAsync(user);

        try
        {
            await resetMailer.SendCodeAsync(user.Email ?? request.Email!.Trim(), user.DisplayName, code, cancellationToken);
        }
        catch (Exception ex)
        {
            user.PasswordResetCodeHash = null;
            user.PasswordResetCodeExpiresUtc = null;
            user.PasswordResetCodeAttempts = 0;
            await userManager.UpdateAsync(user);
            return new ForgotPasswordResponse(false, ClientErrors.ForException(ex));
        }

        return new ForgotPasswordResponse(true, sent);
    }

    public async Task<LoginResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (MicrosoftSignInEnabled)
        {
            return new LoginResponse(false, false, null, MicrosoftSignInMessage);
        }
        var user = await FindByEmailAsync(request.Email, null, cancellationToken);
        if (user is null || !IsResetCodeValid(user, request.Code))
        {
            if (user is not null && !string.IsNullOrWhiteSpace(user.PasswordResetCodeHash))
            {
                user.PasswordResetCodeAttempts++;
                if (user.PasswordResetCodeAttempts >= 5)
                {
                    user.PasswordResetCodeHash = null;
                }

                await userManager.UpdateAsync(user);
            }

            return new LoginResponse(false, false, null, "That code is not valid. Request a new one.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return new LoginResponse(false, false, null, ClientErrors.PasswordRules);
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
        {
            return new LoginResponse(false, false, null, ClientErrors.ForIdentityCodes(result.Errors.Select(e => e.Code)));
        }

        user.MustChangePassword = false;
        user.PasswordResetCodeHash = null;
        user.PasswordResetCodeExpiresUtc = null;
        user.PasswordResetCodeAttempts = 0;
        await userManager.UpdateAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.CreateToken(user.Id, user.UserName!, roles);
        return new LoginResponse(true, false, token, null);
    }

    public async Task<LoginResponse> SignInWithMicrosoftAsync(MicrosoftSessionRequest request, CancellationToken cancellationToken = default)
    {
        if (!MicrosoftSignInEnabled)
        {
            return new LoginResponse(false, false, null, "Microsoft sign-in is not enabled.");
        }

        EntraIdTokenPrincipal principal;
        try
        {
            principal = await entraIdTokenValidator.ValidateAsync(request.IdToken, request.Nonce, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return new LoginResponse(false, false, null, "We could not verify your Microsoft sign-in. Try again.");
        }

        var emailOptionsValue = emailOptions.Value;
        var staffRows = await db.Users.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var rows = staffRows.Select(user => new EntraStaffAccountMatcher.StaffRow(
            user.Id,
            user.UserName,
            user.Email,
            user.EntraUpn,
            user.EntraObjectId,
            FoodBevEmailAddressResolver.Resolve(user, emailOptionsValue))).ToList();

        var match = EntraStaffAccountMatcher.Match(rows, principal);
        if (match.Outcome == EntraStaffMatchOutcome.Ambiguous)
        {
            return new LoginResponse(false, false, null, "This Microsoft account matches more than one staff record. Contact IT.");
        }

        if (match.Outcome == EntraStaffMatchOutcome.NotFound || string.IsNullOrEmpty(match.UserId))
        {
            return new LoginResponse(false, false, null, "This Microsoft account is not on the petty cash list.");
        }

        var appUser = await userManager.FindByIdAsync(match.UserId).ConfigureAwait(false);
        if (appUser is null)
        {
            return new LoginResponse(false, false, null, "This Microsoft account is not on the petty cash list.");
        }

        if (string.IsNullOrWhiteSpace(appUser.EntraObjectId))
        {
            appUser.EntraObjectId = principal.ObjectId;
        }

        if (string.IsNullOrWhiteSpace(appUser.EntraUpn))
        {
            var preferred = principal.PreferredUsername ?? principal.Email;
            var username = appUser.UserName?.Trim();
            var hasOverride = !string.IsNullOrWhiteSpace(username)
                && emailOptionsValue.AddressOverrides.ContainsKey(username);
            if (!hasOverride && !string.IsNullOrWhiteSpace(preferred) && preferred.Contains('@', StringComparison.Ordinal))
            {
                appUser.EntraUpn = preferred.Trim();
            }
        }

        appUser.MustChangePassword = false;
        await userManager.UpdateAsync(appUser).ConfigureAwait(false);
        await ClearFailuresAsync(appUser).ConfigureAwait(false);

        var roles = await userManager.GetRolesAsync(appUser).ConfigureAwait(false);
        var token = tokenService.CreateToken(appUser.Id, appUser.UserName!, roles);
        return new LoginResponse(true, false, token, null);
    }

    private static bool IsResetCodeValid(AppUser user, string? code)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordResetCodeHash)
            || user.PasswordResetCodeExpiresUtc is null
            || user.PasswordResetCodeExpiresUtc <= DateTime.UtcNow
            || user.PasswordResetCodeAttempts >= 5)
        {
            return false;
        }

        var digits = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != 4)
        {
            return false;
        }

        var expected = Convert.FromHexString(user.PasswordResetCodeHash);
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes($"{user.Id}:{digits}"));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string HashResetCode(string userId, string code)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{code}")));
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
