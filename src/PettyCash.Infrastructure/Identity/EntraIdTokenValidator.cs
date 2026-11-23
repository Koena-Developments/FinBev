using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Identity;
using PettyCash.Application.Options;

namespace PettyCash.Infrastructure.Identity;

public sealed class EntraIdTokenValidator(IOptions<MicrosoftSignInOptions> options) : IEntraIdTokenValidator
{
    private readonly MicrosoftSignInOptions _options = options.Value;

    public async Task<EntraIdTokenPrincipal> ValidateAsync(string idToken, string nonce, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("Microsoft sign-in is not configured.");
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new SecurityTokenException("Missing identity token.");
        }

        if (string.IsNullOrWhiteSpace(nonce))
        {
            throw new SecurityTokenException("Missing nonce.");
        }

        var tenantId = _options.TenantId!.Trim();
        var clientId = _options.ClientId!.Trim();
        var metadataAddress = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever());

        var configuration = await manager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers =
            [
                $"https://login.microsoftonline.com/{tenantId}/v2.0",
                $"https://sts.windows.net/{tenantId}/"
            ],
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(idToken, parameters, out _);

        var tokenNonce = principal.FindFirstValue("nonce");
        if (!string.Equals(tokenNonce, nonce, StringComparison.Ordinal))
        {
            throw new SecurityTokenException("Invalid nonce.");
        }

        var tid = principal.FindFirstValue("tid") ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid");
        if (!string.Equals(tid, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("This account is not from your organisation.");
        }

        var oid = principal.FindFirstValue("oid")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(oid))
        {
            throw new SecurityTokenException("The Microsoft token did not include a user id.");
        }

        return new EntraIdTokenPrincipal
        {
            ObjectId = oid,
            TenantId = tenantId,
            Email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email"),
            PreferredUsername = principal.FindFirstValue("preferred_username")
        };
    }
}