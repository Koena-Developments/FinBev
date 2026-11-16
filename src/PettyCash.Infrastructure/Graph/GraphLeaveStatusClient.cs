using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using PettyCash.Application.Abstractions;

namespace PettyCash.Infrastructure.Graph;

public class GraphLeaveStatusClient(IConfiguration configuration, ILogger<GraphLeaveStatusClient> logger) : ILeaveStatusClient
{
    public async Task<LeaveStatusResult> GetLeaveStatusAsync(string? entraUpn, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entraUpn))
        {
            return new LeaveStatusResult(false, true, "NoUpn");
        }

        var tenantId = configuration["Graph:TenantId"];
        var clientId = configuration["Graph:ClientId"];
        var clientSecret = configuration["Graph:ClientSecret"];
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return new LeaveStatusResult(false, true, "GraphNotConfigured");
        }

        try
        {
            return await ReadLeaveAsync(tenantId, clientId, clientSecret, entraUpn, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read leave status for {Upn}. The approver is treated as available.", entraUpn);
            return new LeaveStatusResult(false, false, "GraphError");
        }
    }

    private static async Task<LeaveStatusResult> ReadLeaveAsync(
        string tenantId,
        string clientId,
        string clientSecret,
        string entraUpn,
        CancellationToken cancellationToken)
    {
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var graph = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);

        var user = await graph.Users[entraUpn].GetAsync(cancellationToken: cancellationToken);
        if (user?.Id is null)
        {
            return new LeaveStatusResult(false, true, "UserNotFound");
        }

        var mailbox = await graph.Users[user.Id].MailboxSettings.GetAsync(cancellationToken: cancellationToken);
        if (mailbox?.AutomaticRepliesSetting?.Status == AutomaticRepliesStatus.AlwaysEnabled)
        {
            return new LeaveStatusResult(true, true, "MailboxAutoReply");
        }

        if (mailbox?.AutomaticRepliesSetting?.Status == AutomaticRepliesStatus.Scheduled)
        {
            var start = mailbox.AutomaticRepliesSetting.ScheduledStartDateTime?.ToDateTimeOffset();
            var end = mailbox.AutomaticRepliesSetting.ScheduledEndDateTime?.ToDateTimeOffset();
            var now = DateTimeOffset.UtcNow;
            if (start <= now && end >= now)
            {
                return new LeaveStatusResult(true, true, "MailboxAutoReply");
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDay = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
        var endDay = startDay.AddDays(1);

        var events = await graph.Users[user.Id].CalendarView.GetAsync(config =>
        {
            config.QueryParameters.StartDateTime = startDay.ToString("o");
            config.QueryParameters.EndDateTime = endDay.ToString("o");
            config.QueryParameters.Select = ["showAs", "subject"];
            config.QueryParameters.Top = 50;
        }, cancellationToken);

        var oof = events?.Value?.Any(e => e.ShowAs == FreeBusyStatus.Oof) == true;
        return new LeaveStatusResult(oof, true, oof ? "CalendarOof" : "Available");
    }
}
