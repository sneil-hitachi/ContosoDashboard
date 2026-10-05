using ContosoDashboard.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.TestInfrastructure;

public sealed class SqlServerLocalDbFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"ContosoDashboardTests_{Guid.NewGuid():N}";

    public SqlServerLocalDbFixture(bool ensureCreated = true)
    {
        EnsureCreated = ensureCreated;
    }

    public bool EnsureCreated { get; }

    public string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        if (EnsureCreated)
        {
            await using var context = CreateContext();
            await context.Database.EnsureCreatedAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}