using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

public sealed class ProjectDocumentAuthorizationTests
{
    [Fact]
    public async Task MembersAndManagersSeeProjectDocumentsButPersonalFilesStayPrivate()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        var projectDocument = AddDocument(context, ownerId: 4, projectId: 1, category: "Project Documents", title: "Project file");
        var personalDocument = AddDocument(context, ownerId: 2, projectId: 1, category: "Personal Files", title: "Manager personal file");
        await context.SaveChangesAsync();
        await using var storage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner());

        var memberDocuments = await service.SearchAsync(TestPrincipalFactory.Create(userId: 4), new DocumentQueryOptions());
        var managerDocuments = await service.SearchAsync(TestPrincipalFactory.Create(userId: 2), new DocumentQueryOptions());

        Assert.Contains(memberDocuments, document => document.DocumentId == projectDocument.DocumentId);
        Assert.DoesNotContain(memberDocuments, document => document.DocumentId == personalDocument.DocumentId);
        Assert.Contains(managerDocuments, document => document.DocumentId == projectDocument.DocumentId);
        Assert.Contains(managerDocuments, document => document.DocumentId == personalDocument.DocumentId);
    }

    [Fact]
    public async Task RemovingProjectMembershipRevokesSubsequentDocumentAccess()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        var projectDocument = AddDocument(context, ownerId: 2, projectId: 1, category: "Project Documents", title: "Member-visible file");
        await context.SaveChangesAsync();
        await using var storage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner());
        var member = TestPrincipalFactory.Create(userId: 4);

        Assert.NotNull(await service.GetAuthorizedDocumentAsync(member, projectDocument.DocumentId));

        var membership = await context.ProjectMembers.SingleAsync(item => item.ProjectId == 1 && item.UserId == 4);
        context.ProjectMembers.Remove(membership);
        await context.SaveChangesAsync();

        Assert.Null(await service.GetAuthorizedDocumentAsync(member, projectDocument.DocumentId));
    }

    private static Document AddDocument(ApplicationDbContext context, int ownerId, int? projectId, string category, string title)
    {
        var document = new Document
        {
            Title = title,
            Category = category,
            OriginalFileName = $"{title}.pdf",
            FilePath = $"files/{Guid.NewGuid():N}.pdf",
            FileSize = 3,
            FileType = "application/pdf",
            UploadedAtUtc = DateTime.UtcNow,
            UploadedByUserId = ownerId,
            ProjectId = projectId
        };
        context.Documents.Add(document);
        return document;
    }
}