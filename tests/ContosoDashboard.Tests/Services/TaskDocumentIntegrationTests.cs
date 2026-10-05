using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class TaskDocumentIntegrationTests
{
    [Fact]
    public async Task AttachmentRequiresTaskAndDocumentAccessMatchingProjectAndRejectsDuplicates()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner());
        var member = TestPrincipalFactory.Create(userId: 4);
        var upload = Assert.Single(await service.UploadAsync(member,
            [CreateRequest("project.pdf", "Project Documents", projectId: 1)]));
        var projectDocumentId = upload.DocumentId!.Value;
        var personal = Assert.Single(await service.UploadAsync(member, [CreateRequest("private.pdf", "Personal Files")]));

        Assert.True(await service.AttachToTaskAsync(member, 1, projectDocumentId));
        Assert.False(await service.AttachToTaskAsync(member, 1, projectDocumentId));
        Assert.False(await service.AttachToTaskAsync(member, 1, personal.DocumentId!.Value));
        Assert.False(await service.AttachToTaskAsync(TestPrincipalFactory.Create(userId: 1), 1, projectDocumentId));
        Assert.Single(await service.GetTaskDocumentsAsync(member, 1));
    }

    private static DocumentUploadRequest CreateRequest(string fileName, string category, int? projectId = null)
    {
        return new DocumentUploadRequest
        {
            OriginalFileName = fileName,
            Title = fileName,
            Category = category,
            ProjectId = projectId,
            Content = new MemoryStream([1, 2, 3])
        };
    }
}