using ContosoDashboard.Data;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

public sealed class DocumentUploadIntegrationTests
{
    [Fact]
    public async Task ThreatResultNeverPersistsOrPublishesTheStagedFile()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var temporaryStorage = new TemporaryFileStorage();
        var scanner = new FakeMalwareScanner((path, _) =>
        {
            Assert.Empty(context.Documents);
            Assert.Empty(Directory.GetFiles(temporaryStorage.AcceptedPath));
            Assert.StartsWith(temporaryStorage.StagingPath, path, StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(FakeScanOutcome.ThreatDetected);
        });
        var service = new DocumentService(context, new LocalFileStorageService(temporaryStorage.RootPath), scanner);

        var result = (await service.UploadAsync(
            TestPrincipalFactory.Create(userId: 4),
            [new DocumentUploadRequest
            {
                OriginalFileName = "unsafe.pdf",
                Title = "Unsafe file",
                Category = "Reports",
                Content = new MemoryStream([1, 2, 3])
            }])).Single();

        Assert.False(result.Success);
        Assert.Empty(await context.Documents.ToListAsync());
        Assert.Empty(Directory.GetFiles(temporaryStorage.StagingPath));
        Assert.Empty(Directory.GetFiles(temporaryStorage.AcceptedPath));
    }

    [Fact]
    public async Task PersistenceFailureCompensatesByRemovingThePublishedFile()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(new ThrowOnSaveChangesInterceptor())
            .Options;
        await using var context = new ApplicationDbContext(options);
        await using var temporaryStorage = new TemporaryFileStorage();
        var service = new DocumentService(
            context,
            new LocalFileStorageService(temporaryStorage.RootPath),
            new FakeMalwareScanner());

        var result = (await service.UploadAsync(
            TestPrincipalFactory.Create(userId: 4),
            [new DocumentUploadRequest
            {
                OriginalFileName = "valid.pdf",
                Title = "Valid file",
                Category = "Reports",
                Content = new MemoryStream([4, 5, 6])
            }])).Single();

        Assert.False(result.Success);
        Assert.Empty(await context.Documents.ToListAsync());
        Assert.Empty(Directory.GetFiles(temporaryStorage.AcceptedPath));
        Assert.Empty(Directory.GetFiles(temporaryStorage.StagingPath));
    }

    private sealed class ThrowOnSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException("Injected persistence failure.");
        }
    }
}