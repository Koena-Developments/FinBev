namespace PettyCash.Application.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }

    /// <summary>Send through Microsoft Graph (requires Mail.Send app permission and Graph:* credentials).</summary>
    public bool UseMicrosoftGraph { get; set; }

    /// <summary>When true, send with delegated Mail.Send as each portal user who acts (device-code sign-in per user). When false, send as GraphSenderUpn using the client secret.</summary>
    public bool UseDelegatedGraph { get; set; } = true;

    /// <summary>Mailbox UPN to send as when UseMicrosoftGraph is true (e.g. thabang@foodbev.co.za).</summary>
    public string? GraphSenderUpn { get; set; }

    /// <summary>When SMTP/Graph is off, .eml files are written here (relative to the API output folder unless absolute).</summary>
    public string OutboxFolder { get; set; } = "../../secrets/email-outbox";

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "pettycash@foodbev.co.za";

    public string FromName { get; set; } = "FoodBev Petty Cash";

    public string? DefaultEmailDomain { get; set; } = "foodbev.co.za";

    /// <summary>Portal username → work email when it does not follow the default pattern.</summary>
    public Dictionary<string, string> AddressOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When set, all outbound mail is sent here (useful in development).</summary>
    public string? RedirectAllTo { get; set; }

    public string PublicAppBaseUrl { get; set; } = "http://localhost:3000";
}
