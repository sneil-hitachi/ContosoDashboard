using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class DocumentSharingTests
{
    [Fact]
    public async Task OwnerCanGrantAndRevokeUserAndDepartmentAccessIncludingPersonalFiles()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = CreateService(context, storage, new NotificationService(context));
        var owner = TestPrincipalFactory.Create(userId: 2);
        var result = Assert.Single(await service.UploadAsync(owner,
        [
            new DocumentUploadRequest
            {
                OriginalFileName = "private.txt",
                Title = "Private notes",
                Category = "Personal Files",
                Content = new MemoryStream([1, 2])
            }
        ]));
        var documentId = result.DocumentId!.Value;

        var userGrant = await service.CreateShareAsync(owner, documentId, new DocumentShareGrantRequest(RecipientUserId: 4));
        Assert.True(userGrant.Success, userGrant.Error);
        Assert.True((await service.GetAuthorizedDocumentAsync(TestPrincipalFactory.Create(userId: 4), documentId)) is not null);

        var duplicate = await service.CreateShareAsync(owner, documentId, new DocumentShareGrantRequest(RecipientUserId: 4));
        Assert.False(duplicate.Success);
        var teamGrant = await service.CreateShareAsync(owner, documentId, new DocumentShareGrantRequest(RecipientDepartment: "Engineering"));
        Assert.True(teamGrant.Success, teamGrant.Error);
        Assert.True((await service.GetAuthorizedDocumentAsync(TestPrincipalFactory.Create(userId: 3), documentId)) is not null);
        Assert.Equal(2, await context.Notifications.CountAsync(notification => notification.UserId == 4));

        Assert.False((await service.CreateShareAsync(TestPrincipalFactory.Create(userId: 3), documentId,
            new DocumentShareGrantRequest(RecipientUserId: 2))).Success);
        Assert.True(await service.RevokeShareAsync(owner, documentId, userGrant.ShareId!.Value));
        var teamShare = Assert.Single(await service.GetDocumentSharesAsync(owner, documentId));
        Assert.True(await service.RevokeShareAsync(owner, documentId, teamShare.DocumentShareId));
        Assert.Null(await service.GetAuthorizedDocumentAsync(TestPrincipalFactory.Create(userId: 4), documentId));
    }

    private static DocumentService CreateService(
        ApplicationDbContext context,
        TemporaryFileStorage storage,
        INotificationService notificationService)
    {
        return new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner(), notificationService);
    }
}