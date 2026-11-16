namespace PettyCash.Application.Options;

public sealed class PettyCashFormOptions
{
    public const string SectionName = "PettyCashForm";

    /// <summary>Usernames who receive the signed form when it reaches Finance (after department SM approval).</summary>
    public string[] FinanceDocumentCopyUsernames { get; set; } =
        ["boitumelo.moripe", "busisiwe.ntuli"];
}
