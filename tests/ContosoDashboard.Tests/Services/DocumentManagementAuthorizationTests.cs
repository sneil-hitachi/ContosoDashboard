using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class DocumentManagementAuthorizationTests
{
    [Fact]
    public async Task OwnerAndProjectManagerCanEditMetadataButOtherEmployeesCannot()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = CreateService(context, storage, new FakeMalwareScanner());
        var personalFile = CreateRequest("guide.pdf", [1, 2, 3]) with { Category = "Personal Files" };
        var uploaded = Assert.Single(await service.UploadAsync(TestPrincipalFactory.Create(userId: 4), [personalFile]))
            .DocumentId!.Value;
        var edit = new DocumentMetadataUpdateRequest("Revised guide", "Reports", "Updated", null, []);

        Assert.False((await service.UpdateMetadataAsync(TestPrincipalFactory.Create(userId: 3), uploaded, edit)).Success);
        Assert.True((await service.UpdateMetadataAsync(TestPrincipalFactory.Create(userId: 4), uploaded, edit)).Success);

        var projectDocument = await service.UploadAsync(TestPrincipalFactory.Create(userId: 4),
        [
            CreateRequest("project.pdf", [4, 5, 6]) with
            {
                ProjectId = 1,
                Category = "Project Documents"
            }
        ]);
        var projectId = Assert.Single(projectDocument).DocumentId!.Value;
        Assert.True((await service.UpdateMetadataAsync(TestPrincipalFactory.Create(userId: 2, role: UserRole.ProjectManager.ToString()),
            projectId, edit)).Success);
    }

    [Fact]
    public async Task ThreatRejectedReplacementPreservesCurrentAcceptedFile()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var outcome = FakeScanOutcome.Clean;
        var scanner = new FakeMalwareScanner((_, _) => Task.FromResult(outcome));
        var service = CreateService(context, storage, scanner);
        var principal = TestPrincipalFactory.Create(userId: 4);
        var documentId = Assert.Single(await service.UploadAsync(principal, [CreateRequest("current.pdf", [1, 2, 3])]))
            .DocumentId!.Value;

        outcome = FakeScanOutcome.ThreatDetected;
        var replacement = await service.ReplaceFileAsync(principal, documentId, CreateRequest("replacement.pdf", [9, 8, 7]));

        Assert.False(replacement.Success);
        var content = await service.GetDocumentContentAsync(principal, documentId, preview: false);
        Assert.True(content.Found);
        await using var stream = Assert.IsAssignableFrom<Stream>(content.Content);
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes.ToArray());
        Assert.Equal("current.pdf", content.FileName);
        Assert.Single(await context.Documents.ToListAsync());
    }

    private static DocumentService CreateService(
        ApplicationDbContext context,
        TemporaryFileStorage storage,
        FakeMalwareScanner scanner)
    {
        return new DocumentService(context, new LocalFileStorageService(storage.RootPath), scanner);
    }

    private static DocumentUploadRequest CreateRequest(string fileName, byte[] content)
    {
        return new DocumentUploadRequest
        {
            OriginalFileName = fileName,
            Title = Path.GetFileNameWithoutExtension(fileName),
            Category = "Other",
            Content = new MemoryStream(content)
        };
    }
}