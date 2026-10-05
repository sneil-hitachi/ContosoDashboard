using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class DocumentAuditTests
{
    [Fact]
    public async Task DocumentOperationsAreAuditedAndDeletionRetainsSnapshots()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner(),
            new NotificationService(context));
        var owner = TestPrincipalFactory.Create(userId: 4, name: "Ni Kang");
        var uploaded = Assert.Single(await service.UploadAsync(owner, [CreateRequest("audit.pdf", [1, 2, 3]) ]));
        var documentId = uploaded.DocumentId!.Value;

        var content = await service.GetDocumentContentAsync(owner, documentId, preview: false);
        Assert.True(content.Found);
        await content.Content!.DisposeAsync();
        Assert.True((await service.UpdateMetadataAsync(owner, documentId,
            new DocumentMetadataUpdateRequest("Audited guide", "Reports", "Updated", null, []))).Success);
        Assert.True((await service.ReplaceFileAsync(owner, documentId, CreateRequest("replacement.pdf", [4, 5, 6]))).Success);
        Assert.True((await service.CreateShareAsync(owner, documentId, new DocumentShareGrantRequest(RecipientUserId: 2))).Success);
        Assert.True((await service.DeleteAsync(owner, documentId, confirmed: true)).Success);

        var activity = await service.GetActivityAsync(TestPrincipalFactory.Create(userId: 1, role: UserRole.Administrator.ToString()));
        Assert.Contains(activity, item => item.Action == "Uploaded" && item.DocumentId == documentId);
        Assert.Contains(activity, item => item.Action == "Downloaded" && item.DocumentId == documentId);
        Assert.Contains(activity, item => item.Action == "MetadataUpdated" && item.DocumentId == documentId);
        Assert.Contains(activity, item => item.Action == "Replaced" && item.DocumentId == documentId);
        Assert.Contains(activity, item => item.Action == "Shared" && item.DocumentId == documentId);
        var deleted = Assert.Single(activity, item => item.Action == "Deleted");
        Assert.Equal(documentId, deleted.DocumentId);
        Assert.Equal("Audited guide", deleted.DocumentTitleSnapshot);
        Assert.Equal(4, deleted.ActorUserId);
        Assert.Equal("Ni Kang", deleted.ActorNameSnapshot);
        Assert.True(deleted.OccurredAtUtc.Kind is DateTimeKind.Utc or DateTimeKind.Unspecified);
        Assert.Empty(await context.Documents.ToListAsync());
    }

    private static DocumentUploadRequest CreateRequest(string fileName, byte[] bytes)
    {
        return new DocumentUploadRequest
        {
            OriginalFileName = fileName,
            Title = "Audit guide",
            Category = "Other",
            Content = new MemoryStream(bytes)
        };
    }
}