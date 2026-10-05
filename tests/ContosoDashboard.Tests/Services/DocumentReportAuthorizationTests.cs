using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class DocumentReportAuthorizationTests
{
    [Fact]
    public async Task ReportsAggregateTypesUploadersAndAccessAndAreAdministratorOnly()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner());
        var owner = TestPrincipalFactory.Create(userId: 4);
        var upload = Assert.Single(await service.UploadAsync(owner, [new DocumentUploadRequest
        {
            OriginalFileName = "report.pdf",
            Title = "Quarterly report",
            Category = "Reports",
            Content = new MemoryStream([1])
        }]));
        var content = await service.GetDocumentContentAsync(owner, upload.DocumentId!.Value, preview: false);
        await content.Content!.DisposeAsync();

        var denied = TestPrincipalFactory.Create(userId: 4);
        Assert.Empty(await service.GetActivityAsync(denied));
        Assert.Null(await service.GetReportAsync(denied));

        var report = await service.GetReportAsync(TestPrincipalFactory.Create(userId: 1, role: UserRole.Administrator.ToString()));
        Assert.NotNull(report);
        Assert.Contains(report.FileTypes, item => item.FileType == "application/pdf" && item.Count == 1);
        Assert.Contains(report.Uploaders, item => item.UserId == 4 && item.UploadCount == 1);
        Assert.Contains(report.AccessPatterns, item => item.Action == "Downloaded" && item.Count == 1);
    }
}