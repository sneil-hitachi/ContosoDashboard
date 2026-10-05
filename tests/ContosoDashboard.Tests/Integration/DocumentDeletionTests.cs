using ContosoDashboard.Data;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

public sealed class DocumentDeletionTests
{
    [Fact]
    public async Task CancellationChangesNothingAndConfirmedOwnerDeletionRemovesContent()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner());
        var principal = TestPrincipalFactory.Create(userId: 4);
        var upload = Assert.Single(await service.UploadAsync(principal,
        [
            new DocumentUploadRequest
            {
                OriginalFileName = "remove.txt",
                Title = "Remove me",
                Category = "Personal Files",
                Content = new MemoryStream([3, 2, 1])
            }
        ]));
        var documentId = upload.DocumentId!.Value;
        var key = (await context.Documents.SingleAsync(document => document.DocumentId == documentId)).FilePath;
        var physicalPath = Path.Combine(storage.RootPath, key.Replace('/', Path.DirectorySeparatorChar));

        Assert.False((await service.DeleteAsync(principal, documentId, confirmed: false)).Success);
        Assert.True(File.Exists(physicalPath));
        Assert.NotNull(await service.GetAuthorizedDocumentAsync(principal, documentId));
        Assert.False((await service.DeleteAsync(TestPrincipalFactory.Create(userId: 3), documentId, confirmed: true)).Success);

        Assert.True((await service.DeleteAsync(principal, documentId, confirmed: true)).Success);
        Assert.False(File.Exists(physicalPath));
        Assert.Null(await context.Documents.SingleOrDefaultAsync(document => document.DocumentId == documentId));
    }
}