using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

public sealed class DocumentSecurityRegressionTests
{
    [Fact]
    public async Task StaleMembershipAndRevokedSharesCannotReadContentOrRevealStorageKeys()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner());
        var manager = TestPrincipalFactory.Create(userId: 2, role: UserRole.ProjectManager.ToString());
        var member = TestPrincipalFactory.Create(userId: 4);
        var projectUpload = Assert.Single(await service.UploadAsync(manager,
        [
            CreateRequest("project.pdf", "Project Documents", 1)
        ]));
        var projectDocumentId = projectUpload.DocumentId!.Value;
        var allowedContent = await service.GetDocumentContentAsync(member, projectDocumentId, preview: false);
        Assert.True(allowedContent.Found);
        Assert.Equal("project.pdf", allowedContent.FileName);
        Assert.DoesNotContain(storage.RootPath, allowedContent.FileName, StringComparison.OrdinalIgnoreCase);
        await allowedContent.Content!.DisposeAsync();

        var membership = await context.ProjectMembers.SingleAsync(item => item.ProjectId == 1 && item.UserId == 4);
        context.ProjectMembers.Remove(membership);
        await context.SaveChangesAsync();
        Assert.Null(await service.GetAuthorizedDocumentAsync(member, projectDocumentId));
        Assert.False((await service.GetDocumentContentAsync(member, projectDocumentId, preview: false)).Found);
        Assert.Null(await service.GetAuthorizedDocumentAsync(member, int.MaxValue));

        var personalUpload = Assert.Single(await service.UploadAsync(manager,
        [
            CreateRequest("shared.txt", "Personal Files")
        ]));
        var personalDocumentId = personalUpload.DocumentId!.Value;
        var grant = await service.CreateShareAsync(manager, personalDocumentId, new DocumentShareGrantRequest(RecipientUserId: 4));
        Assert.True(grant.Success, grant.Error);
        Assert.NotNull(await service.GetAuthorizedDocumentAsync(member, personalDocumentId));
        Assert.True(await service.RevokeShareAsync(manager, personalDocumentId, grant.ShareId!.Value));
        Assert.Null(await service.GetAuthorizedDocumentAsync(member, personalDocumentId));
        Assert.False((await service.GetDocumentContentAsync(member, personalDocumentId, preview: false)).Found);

        Assert.Empty(Directory.EnumerateFiles(Path.Combine(storage.RootPath, "staging")));
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