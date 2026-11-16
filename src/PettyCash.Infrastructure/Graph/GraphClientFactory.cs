using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;

namespace PettyCash.Infrastructure.Graph;

internal static class GraphClientFactory
{
    public static GraphServiceClient? TryCreate(IConfiguration configuration)
    {
        var tenantId = configuration["Graph:TenantId"];
        var clientId = configuration["Graph:ClientId"];
        var clientSecret = configuration["Graph:ClientSecret"];
        if (string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }
}
