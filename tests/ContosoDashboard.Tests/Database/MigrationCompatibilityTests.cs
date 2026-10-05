using ContosoDashboard.Data;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ContosoDashboard.Tests.Database;

public sealed class MigrationCompatibilityTests
{
    [Fact]
    public async Task FreshDatabaseIsInitializedWithSeedData()
    {
        await using var fixture = new SqlServerLocalDbFixture(ensureCreated: false);
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();

        await DatabaseInitializer.InitializeAsync(context);

        Assert.Equal(4, await context.Users.CountAsync());
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task ExistingEnsureCreatedDatabaseIsAdoptedWithoutLosingTrainingData()
    {
        await using var fixture = new SqlServerLocalDbFixture(ensureCreated: false);
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(DatabaseInitializer.InitialSchemaMigrationId);
        await context.Database.ExecuteSqlRawAsync("DROP TABLE dbo.__EFMigrationsHistory");
        var employee = await context.Users.SingleAsync(user => user.UserId == 4);
        employee.DisplayName = "Retained Training User";
        await context.SaveChangesAsync();

        await DatabaseInitializer.InitializeAsync(context);

        Assert.Equal("Retained Training User", (await context.Users.FindAsync(4))?.DisplayName);
        Assert.Equal(4, await context.Users.CountAsync());
    }

    [Fact]
    public async Task ExistingUnrecognizedSchemaIsRejectedWithoutDroppingItsTables()
    {
        await using var fixture = new SqlServerLocalDbFixture(ensureCreated: false);
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.UserData (Id int NOT NULL PRIMARY KEY, Value nvarchar(100) NOT NULL)");
        await context.Database.ExecuteSqlRawAsync("INSERT INTO dbo.UserData (Id, Value) VALUES (1, N'keep')");

        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseInitializer.InitializeAsync(context));

        Assert.True(await context.Database.CanConnectAsync());
        Assert.Equal(1, await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM dbo.UserData").SingleAsync());
    }
}