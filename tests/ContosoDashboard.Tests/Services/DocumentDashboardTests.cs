using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class DocumentDashboardTests
{
    [Fact]
    public async Task DashboardReturnsOnlyCurrentUsersRecentFiveDocumentsAndCount()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        for (var index = 0; index < 7; index++)
        {
            context.Documents.Add(NewDocument(4, $"User file {index}", DateTime.UtcNow.AddMinutes(index)));
        }
        context.Documents.Add(NewDocument(3, "Other user's file", DateTime.UtcNow.AddDays(1)));
        await context.SaveChangesAsync();

        var summary = await new DashboardService(context).GetDashboardSummaryAsync(4);

        Assert.Equal(7, summary.DocumentCount);
        Assert.Equal(5, summary.RecentDocuments.Count);
        Assert.Equal(new[] { "User file 6", "User file 5", "User file 4", "User file 3", "User file 2" },
            summary.RecentDocuments.Select(document => document.Title));
        Assert.DoesNotContain(summary.RecentDocuments, document => document.UploadedByUserId != 4);
    }

    [Fact]
    public async Task ProjectUploadNotifiesOtherCurrentProjectMembers()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var storage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner(),
            new NotificationService(context));
        var result = Assert.Single(await service.UploadAsync(TestPrincipalFactory.Create(userId: 4),
        [
            new DocumentUploadRequest
            {
                OriginalFileName = "project.pdf",
                Title = "Project upload",
                Category = "Project Documents",
                ProjectId = 1,
                Content = new MemoryStream([4, 5, 6])
            }
        ]));

        Assert.True(result.Success, result.Error);
        Assert.Single(await context.Notifications.Where(notification => notification.UserId == 3).ToListAsync());
        Assert.Empty(await context.Notifications.Where(notification => notification.UserId == 2).ToListAsync());
    }

    private static Document NewDocument(int ownerId, string title, DateTime uploadedAtUtc)
    {
        return new Document
        {
            Title = title,
            Category = "Other",
            OriginalFileName = $"{title}.pdf",
            FilePath = $"files/{Guid.NewGuid():N}.pdf",
            FileSize = 1,
            FileType = "application/pdf",
            UploadedAtUtc = uploadedAtUtc,
            UploadedByUserId = ownerId
        };
    }
}