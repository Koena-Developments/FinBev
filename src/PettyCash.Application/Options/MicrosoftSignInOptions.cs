namespace PettyCash.Application.Options;

public sealed class MicrosoftSignInOptions
{
    public const string SectionName = "Auth:Microsoft";

    public string? TenantId { get; set; }
    public string? ClientId { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);
}