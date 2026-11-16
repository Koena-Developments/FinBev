using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PettyCash.Infrastructure.Persistence;

public class PettyCashDbContextFactory : IDesignTimeDbContextFactory<PettyCashDbContext>
{
    public PettyCashDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(basePath, "src", "PettyCash.Api");
        if (!Directory.Exists(apiPath))
        {
            apiPath = Path.GetFullPath(Path.Combine(basePath, "..", "PettyCash.Api"));
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(apiPath) ? apiPath : basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=pettycash;Username=pettycash;Password=pettycash_dev"; // project dev instance (scripts/init-dev-postgres.ps1)

        var options = new DbContextOptionsBuilder<PettyCashDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PettyCashDbContext(options);
    }
}
