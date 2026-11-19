using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PettyCash.Infrastructure.Security;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Kiota.Authentication.Azure;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace PettyCash.Infrastructure.Graph;

public sealed class GraphMailboxTokenProvider(
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<GraphMailboxTokenProvider> logger)
{
    private readonly HttpClient _http = new();
    private readonly ConcurrentDictionary<string, PendingDeviceCode> _pending = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshGates = new();
    private readonly ConcurrentDictionary<string, CachedAccessToken> _accessTokens = new();

    private readonly string _tenantId = configuration["Graph:TenantId"]
        ?? throw new InvalidOperationException("Microsoft 365 mailbox connection is not set up. Contact IT.");
    private readonly string _clientId = configuration["Graph:ClientId"]
        ?? throw new InvalidOperationException("Microsoft 365 mailbox connection is not set up. Contact IT.");

    /// <summary>
    /// Send <c>client_secret</c> on device-code and refresh_token requests only when this is true.
    /// Public clients must not send a secret (AADSTS700025); confidential clients need it (AADSTS7000218).
    /// <c>Graph:ClientSecret</c> may still be set for other features (e.g. app-only Graph) without
    /// affecting delegated mailbox OAuth when this flag is false.
    /// </summary>
    private readonly string? _delegatedTokenClientSecret =
        configuration.GetValue("Graph:DelegatedOAuthUsesClientSecret", false)
        && !string.IsNullOrWhiteSpace(configuration["Graph:ClientSecret"])
            ? configuration["Graph:ClientSecret"]
            : null;

    private static readonly string[] MailScopes =
    [
        "openid",
        "profile",
        "offline_access",
        "https://graph.microsoft.com/Mail.Send",
        "https://graph.microsoft.com/User.Read"
    ];

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string TokenFolder => GraphDelegatedTokenPaths.TokenFolder(configuration, environment);

    public bool HasMailboxConnected(string userId)
    {
        var path = GraphDelegatedTokenPaths.CacheFilePath(TokenFolder, userId);
        return File.Exists(path);
    }

    public IReadOnlyList<string> ConnectedUserIds()
    {
        if (!Directory.Exists(TokenFolder))
        {
            return [];
        }

        return Directory.EnumerateFiles(TokenFolder, "*.msal")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList()!;
    }

    public async Task<DeviceCodeStartResult> StartDeviceCodeAsync(string userId, CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["scope"] = string.Join(' ', MailScopes)
        };
        // Device authorization request uses client_id + scope only; the confidential client
        // authenticates when redeeming the device code at /token (poll + refresh).

        var response = await _http.PostAsync(
                $"{Authority()}/oauth2/v2.0/devicecode",
                new FormUrlEncodedContent(form),
                cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadFromJsonAsync<DeviceCodeResponse>(Json, cancellationToken)
            .ConfigureAwait(false);
        if (body is null || string.IsNullOrWhiteSpace(body.UserCode) || string.IsNullOrWhiteSpace(body.DeviceCode))
        {
            logger.LogWarning("Device code request failed with status {StatusCode}", (int)response.StatusCode);
            throw new InvalidOperationException("We could not start Microsoft 365 sign-in. Try again in a moment.");
        }

        var verificationUri = body.VerificationUri ?? "https://microsoft.com/devicelogin";
        _pending[userId] = new PendingDeviceCode(
            body.DeviceCode,
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(body.ExpiresIn, 60)));
        return new DeviceCodeStartResult(body.UserCode, verificationUri, body.ExpiresIn, body.Interval ?? 5, body.Message ?? string.Empty);
    }

    /// <summary>Single token poll; call repeatedly from the client until Success or Fatal.</summary>
    public async Task<DeviceCodePollResult> PollDeviceCodeAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_pending.TryGetValue(userId, out var pending) || pending.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _pending.TryRemove(userId, out _);
            return new DeviceCodePollResult("error", "Start the Microsoft 365 mailbox connection again.");
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["client_id"] = _clientId,
            ["device_code"] = pending.DeviceCode
        };
        AddClientSecretIfConfigured(form);

        var response = await _http.PostAsync(
                $"{Authority()}/oauth2/v2.0/token",
                new FormUrlEncodedContent(form),
                cancellationToken)
            .ConfigureAwait(false);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var token = JsonSerializer.Deserialize<TokenResponse>(raw, Json);

        if (token?.Error is "authorization_pending" or "slow_down")
        {
            return new DeviceCodePollResult(token.Error!, null);
        }

        if (token?.Error == "expired_token")
        {
            _pending.TryRemove(userId, out _);
            return new DeviceCodePollResult("error", "The Microsoft 365 sign-in code expired. Start the mailbox connection again.");
        }

        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(token?.RefreshToken))
        {
            _pending.TryRemove(userId, out _);
            var path = GraphDelegatedTokenPaths.CacheFilePath(TokenFolder, userId);
            var payload = new StoredMailboxToken(token.RefreshToken!, DateTimeOffset.UtcNow);
            await SecretFiles.WriteAsync(path, JsonSerializer.Serialize(payload, Json), cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Mailbox OAuth tokens stored for user {UserId}", userId);
            return new DeviceCodePollResult("success", null);
        }

        return new DeviceCodePollResult("error", SafeOAuthError(token));
    }

    public sealed record DeviceCodePollResult(string Status, string? Error);

    public async Task<string> GetAccessTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (TryGetCachedAccessToken(userId, out var cached))
        {
            return cached;
        }

        var gate = _refreshGates.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetCachedAccessToken(userId, out cached))
            {
                return cached;
            }

            var path = GraphDelegatedTokenPaths.CacheFilePath(TokenFolder, userId);
            if (!File.Exists(path))
            {
                throw new MailboxReconnectRequiredException(
                    "Microsoft 365 mailbox is not connected. Use Account → Connect mailbox in the portal.");
            }

            var stored = await ReadStoredTokenAsync(path, cancellationToken).ConfigureAwait(false);
            if (stored is null)
            {
                ForgetMailbox(userId, path);
                throw new MailboxReconnectRequiredException(
                    "Your Microsoft 365 mailbox connection is damaged. Connect it again under Account, then Connect mailbox.");
            }

            var refreshed = await RequestAccessTokenAsync(stored.RefreshToken, cancellationToken).ConfigureAwait(false);
            if (!refreshed.Succeeded && IsDeadRefreshToken(refreshed.Token?.Error))
            {
                var latest = await ReadStoredTokenAsync(path, cancellationToken).ConfigureAwait(false);
                if (latest is not null
                    && !string.Equals(latest.RefreshToken, stored.RefreshToken, StringComparison.Ordinal))
                {
                    stored = latest;
                    refreshed = await RequestAccessTokenAsync(stored.RefreshToken, cancellationToken).ConfigureAwait(false);
                }
            }

            var token = refreshed.Token;
            var accessToken = token?.AccessToken;
            if (!refreshed.Succeeded || token is null || string.IsNullOrWhiteSpace(accessToken))
            {
                logger.LogWarning(
                    "Mailbox token refresh failed for {UserId} with status {StatusCode}: {OAuthError}",
                    userId,
                    refreshed.StatusCode,
                    DescribeTokenError(token));

                if (IsDeadRefreshToken(token?.Error))
                {
                    var current = await ReadStoredTokenAsync(path, cancellationToken).ConfigureAwait(false);
                    if (current is null || string.Equals(current.RefreshToken, stored.RefreshToken, StringComparison.Ordinal))
                    {
                        ForgetMailbox(userId, path);
                    }

                    throw new MailboxReconnectRequiredException(
                        "Connect your Microsoft 365 mailbox again under Account, then Connect mailbox.");
                }

                throw new InvalidOperationException(
                    "Connect your Microsoft 365 mailbox again under Account, then Connect mailbox.");
            }

            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                var updated = new StoredMailboxToken(token.RefreshToken, DateTimeOffset.UtcNow);
                await SecretFiles.WriteAsync(path, JsonSerializer.Serialize(updated, Json), cancellationToken).ConfigureAwait(false);
            }

            var lifetimeSeconds = token.ExpiresIn > 120 ? token.ExpiresIn - 60 : 300;
            _accessTokens[userId] = new CachedAccessToken(accessToken, DateTimeOffset.UtcNow.AddSeconds(lifetimeSeconds));
            return accessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<GraphServiceClient> CreateGraphClientAsync(string userId, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(userId, cancellationToken).ConfigureAwait(false);
        var credential = new StaticAccessTokenCredential(token);
        var authProvider = new AzureIdentityAuthenticationProvider(
            credential,
            allowedHosts: ["graph.microsoft.com"],
            scopes: ["https://graph.microsoft.com/.default"]);
        var adapter = new HttpClientRequestAdapter(authProvider);
        return new GraphServiceClient(adapter);
    }

    private bool TryGetCachedAccessToken(string userId, out string accessToken)
    {
        accessToken = string.Empty;
        if (!_accessTokens.TryGetValue(userId, out var cached) || cached.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        accessToken = cached.AccessToken;
        return true;
    }

    private async Task<StoredMailboxToken?> ReadStoredTokenAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var stored = JsonSerializer.Deserialize<StoredMailboxToken>(json, Json);
            return stored is { RefreshToken.Length: > 0 } ? stored : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<RefreshAttempt> RequestAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _clientId,
            ["refresh_token"] = refreshToken,
            ["scope"] = string.Join(' ', MailScopes)
        };
        AddClientSecretIfConfigured(form);

        var response = await _http.PostAsync(
                $"{Authority()}/oauth2/v2.0/token",
                new FormUrlEncodedContent(form),
                cancellationToken)
            .ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var token = JsonSerializer.Deserialize<TokenResponse>(raw, Json);
        var succeeded = response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(token?.AccessToken);
        return new RefreshAttempt(succeeded, (int)response.StatusCode, token);
    }

    private void ForgetMailbox(string userId, string path)
    {
        _accessTokens.TryRemove(userId, out _);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            logger.LogWarning(
                "Removed the stored Microsoft 365 mailbox token for {UserId}. That person needs to connect the mailbox again under Account.",
                userId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not remove the stored mailbox token for {UserId}", userId);
        }
    }

    private static bool IsDeadRefreshToken(string? error) =>
        error is "invalid_grant" or "interaction_required" or "consent_required";

    private static string DescribeTokenError(TokenResponse? token)
    {
        if (token is null)
        {
            return "no error details";
        }

        var description = token.ErrorDescription;
        if (!string.IsNullOrWhiteSpace(description))
        {
            var trace = description.IndexOf("Trace ID", StringComparison.OrdinalIgnoreCase);
            if (trace >= 0)
            {
                description = description[..trace];
            }

            description = description.Trim().TrimEnd('.');
            if (description.Length > 220)
            {
                description = description[..220];
            }
        }

        var codes = token.ErrorCodes is { Length: > 0 } ? string.Join(',', token.ErrorCodes) : null;
        var detail = string.Join(' ', new[] { token.Error, codes, description }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(detail) ? "no error details" : detail;
    }

    private string Authority() => $"https://login.microsoftonline.com/{_tenantId}";

    private void AddClientSecretIfConfigured(IDictionary<string, string> form)
    {
        if (!string.IsNullOrEmpty(_delegatedTokenClientSecret))
        {
            form["client_secret"] = _delegatedTokenClientSecret;
        }
    }

    public sealed record DeviceCodeStartResult(
        string UserCode,
        string VerificationUri,
        int ExpiresIn,
        int IntervalSeconds,
        string Message);

    private sealed record PendingDeviceCode(string DeviceCode, DateTimeOffset ExpiresAt);

    private static string SafeOAuthError(TokenResponse? token)
    {
        _ = token;
        return "Microsoft 365 sign-in did not finish. Start the mailbox connection again.";
    }

    private sealed record DeviceCodeResponse
    {
        [JsonPropertyName("user_code")]
        public string? UserCode { get; init; }

        [JsonPropertyName("device_code")]
        public string? DeviceCode { get; init; }

        [JsonPropertyName("verification_uri")]
        public string? VerificationUri { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("interval")]
        public int? Interval { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; init; }

        [JsonPropertyName("error_codes")]
        public int[]? ErrorCodes { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed record StoredMailboxToken(string RefreshToken, DateTimeOffset UpdatedAtUtc);

    private sealed record CachedAccessToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

    private readonly record struct RefreshAttempt(bool Succeeded, int StatusCode, TokenResponse? Token);

    private sealed class StaticAccessTokenCredential(string token) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(token, DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1)));
    }
}
