using Microsoft.Extensions.Configuration;
using PettyCash.Application.Options;

namespace PettyCash.Application.Common;

public static class MicrosoftSignInSettings
{
    public static MicrosoftSignInOptions Read(IConfiguration configuration)
    {
        var section = configuration.GetSection(MicrosoftSignInOptions.SectionName);
        return new MicrosoftSignInOptions
        {
            TenantId = section["TenantId"],
            ClientId = section["ClientId"]
        };
    }

    public static bool IsConfigured(IConfiguration configuration) => Read(configuration).IsConfigured;
}