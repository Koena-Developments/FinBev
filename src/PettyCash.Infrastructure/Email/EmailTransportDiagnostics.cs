using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Options;
using PettyCash.Infrastructure.Graph;

namespace PettyCash.Infrastructure.Email;

public static class EmailTransportDiagnostics
{
    public static void LogActiveTransport(IServiceProvider services, ILogger logger)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var emailSection = configuration.GetSection(EmailOptions.SectionName);
        var delegated = emailSection.GetValue<bool>("UseDelegatedGraph")
            && emailSection.GetValue<bool>("UseMicrosoftGraph")
            && !string.IsNullOrWhiteSpace(configuration["Graph:TenantId"])
            && !string.IsNullOrWhiteSpace(configuration["Graph:ClientId"]);
        var appGraph = emailSection.GetValue<bool>("UseMicrosoftGraph")
            && !emailSection.GetValue<bool>("UseDelegatedGraph")
            && GraphClientFactory.TryCreate(configuration) is not null
            && !string.IsNullOrWhiteSpace(emailSection["GraphSenderUpn"]);
        var smtp = emailSection.GetValue<bool>("Enabled")
            && !string.IsNullOrWhiteSpace(emailSection["SmtpHost"]);

        if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Petty cash email transport: local development. Request and reminder emails are not sent.");
            return;
        }

        var sender = services.GetRequiredService<IEmailSender>();
        var mode = sender switch
        {
            DelegatedGraphEmailSender => "Microsoft Graph delegated (each user connects mailbox)",
            GraphEmailSender => "Microsoft Graph app-only (real inbox delivery)",
            SmtpEmailSender => "SMTP (real inbox delivery)",
            OutboxEmailSender => "Outbox .eml files only — configure SMTP or Graph for inbox delivery",
            _ => sender.GetType().Name
        };

        logger.LogInformation(
            "Petty cash email transport: {Mode}. DelegatedGraph={Delegated} AppGraph={AppGraph} SMTP={Smtp} RedirectAllTo={Redirect}",
            mode,
            delegated,
            appGraph,
            smtp,
            emailSection["RedirectAllTo"] ?? "(none)");
    }
}
