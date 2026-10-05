using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class DocumentUploadValidationTests
{
    private static readonly string[] Categories =
    [
        "Project Documents",
        "Team Resources",
        "Personal Files",
        "Reports",
        "Presentations",
        "Other"
    ];

    [Fact]
    public async Task AcceptsAllSixCategoriesAndDerivesMimeTypeFromSupportedExtension()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = CreateService(context, storage, new FakeMalwareScanner());

        foreach (var category in Categories)
        {
            var request = CreateRequest($"{category}.pdf", category, "application/octet-stream");
            var result = (await service.UploadAsync(TestPrincipalFactory.Create(userId: 4), [request])).Single();

            Assert.True(result.Success, result.Error);
        }

        var documents = await context.Documents.OrderBy(document => document.DocumentId).ToListAsync();
        Assert.Equal(Categories, documents.Select(document => document.Category));
        Assert.All(documents, document => Assert.Equal("application/pdf", document.FileType));
    }

    [Fact]
    public async Task AcceptsEverySupportedExtensionAndStoresItsNormalizedMimeType()
    {
        var expectedTypes = new Dictionary<string, string>
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".txt"] = "text/plain",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png"
        };
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = CreateService(context, storage, new FakeMalwareScanner());
        var requests = expectedTypes.Keys.Select(extension => CreateRequest($"supported{extension}", "Reports", "application/octet-stream")).ToArray();

        var results = await service.UploadAsync(TestPrincipalFactory.Create(userId: 4), requests);

        Assert.All(results, result => Assert.True(result.Success, result.Error));
        var documents = await context.Documents.ToListAsync();
        Assert.Equal(expectedTypes.Count, documents.Count);
        foreach (var document in documents)
        {
            Assert.Equal(expectedTypes[Path.GetExtension(document.OriginalFileName)], document.FileType);
        }
    }

    [Fact]
    public async Task RejectsMissingTitleCategoryAndUnsupportedExtension()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = CreateService(context, storage, new FakeMalwareScanner());
        var principal = TestPrincipalFactory.Create(userId: 4);

        var results = await service.UploadAsync(principal,
        [
            CreateRequest("title.pdf", "Reports") with { Title = " " },
            CreateRequest("category.pdf", ""),
            CreateRequest("payload.exe", "Reports")
        ]);

        Assert.Equal(3, results.Count);
        Assert.All(results, result => Assert.False(result.Success));
        Assert.Empty(await context.Documents.ToListAsync());
    }

    [Fact]
    public async Task EnforcesTwentyFiveMegabyteLimitAndKeepsMixedBatchResultsIndependent()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = CreateService(context, storage, new FakeMalwareScanner());
        var principal = TestPrincipalFactory.Create(userId: 4);
        var oversized = CreateRequest("large.pdf", "Reports") with
        {
            Content = new MemoryStream(new byte[DocumentService.MaximumFileSizeBytes + 1])
        };

        var results = await service.UploadAsync(principal,
        [
            CreateRequest("unsupported.exe", "Reports"),
            oversized,
            CreateRequest("accepted.pdf", "Reports")
        ]);

        Assert.Equal(3, results.Count);
        Assert.False(results[0].Success);
        Assert.False(results[1].Success);
        Assert.True(results[2].Success, results[2].Error);
        Assert.Single(await context.Documents.ToListAsync());
    }

    private static DocumentService CreateService(
        ApplicationDbContext context,
        TemporaryFileStorage storage,
        FakeMalwareScanner scanner)
    {
        return new DocumentService(context, new LocalFileStorageService(storage.RootPath), scanner);
    }

    private static DocumentUploadRequest CreateRequest(
        string fileName,
        string category,
        string? contentType = null)
    {
        return new DocumentUploadRequest
        {
            OriginalFileName = fileName,
            Title = Path.GetFileNameWithoutExtension(fileName),
            Category = category,
            ContentType = contentType,
            Content = new MemoryStream([1, 2, 3, 4])
        };
    }
}