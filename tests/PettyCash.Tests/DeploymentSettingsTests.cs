using Npgsql;
using PettyCash.Application.Common;
using PettyCash.Infrastructure.Security;

namespace PettyCash.Tests;

public class DeploymentSettingsTests
{
    [Theory]
    [InlineData("Host=db.internal;Database=pettycash;Username=pettycash;Password=secret")]
    [InlineData("postgresql://pettycash:secret@dpg-abc-a/pettycash")]
    public void Production_urls_require_encryption(string connectionString)
    {
        var normalized = PostgresConnectionStrings.Normalize(connectionString);
        using var connection = new NpgsqlConnection(normalized);
        Assert.True(DeploymentSettings.RequiresEncryption(connection.ConnectionString));
        Assert.False(DeploymentSettings.DisablesSsl(connection.ConnectionString));
    }

    [Fact]
    public void Keeps_encryption_when_the_url_already_requires_it()
    {
        const string connectionString = "Host=db;SSL Mode=Require;Password=secret";
        var normalized = PostgresConnectionStrings.Normalize(connectionString);
        Assert.True(DeploymentSettings.RequiresEncryption(normalized));
    }

    [Theory]
    [InlineData("Secret123")]
    [InlineData("FoodBev2026")]
    public void Accepts_a_staff_first_password(string password)
    {
        Assert.True(DeploymentSettings.IsAcceptableInitialPassword(password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    public void Rejects_a_weak_first_password(string password)
    {
        Assert.False(DeploymentSettings.IsAcceptableInitialPassword(password));
    }
}
