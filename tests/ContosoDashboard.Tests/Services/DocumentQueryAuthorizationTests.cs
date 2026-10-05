using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class DocumentQueryAuthorizationTests
{
    [Fact]
    public async Task SearchFiltersAndSortsOnlyDocumentsAuthorizedForTheCurrentUser()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        var ownerDocument = NewDocument(4, "Architecture notes", "blueprint", "Engineering", null, "Other", 50, DateTime.UtcNow.AddDays(-2));
        ownerDocument.Tags.Add(NewTag("build-plan"));
        var projectDocument = NewDocument(2, "Deployment guide", "cluster runbook", "Engineering", 1, "Project Documents", 100, DateTime.UtcNow.AddDays(-1));
        projectDocument.Tags.Add(NewTag("release-checklist"));
        var sharedPersonalDocument = NewDocument(2, "Shared handbook", "personal reference", "Engineering", null, "Personal Files", 25, DateTime.UtcNow);
        var privateDocument = NewDocument(2, "Private notes", "not shared", "Engineering", null, "Other", 500, DateTime.UtcNow);
        context.Documents.AddRange(ownerDocument, projectDocument, sharedPersonalDocument, privateDocument);
        await context.SaveChangesAsync();
        context.DocumentShares.Add(new DocumentShare
        {
            DocumentId = sharedPersonalDocument.DocumentId,
            GrantedByUserId = 2,
            RecipientUserId = 4,
            GrantedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        await using var storage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(storage.RootPath), new FakeMalwareScanner());
        var principal = TestPrincipalFactory.Create(userId: 4);

        Assert.Single(await service.SearchAsync(principal, new DocumentQueryOptions { SearchTerm = "Architecture" }));
        Assert.Single(await service.SearchAsync(principal, new DocumentQueryOptions { SearchTerm = "blueprint" }));
        Assert.Single(await service.SearchAsync(principal, new DocumentQueryOptions { SearchTerm = "build-plan" }));
        var uploaderMatches = await service.SearchAsync(principal, new DocumentQueryOptions { SearchTerm = "Camille Nicole" });
        Assert.Equal(2, uploaderMatches.Count);
        Assert.DoesNotContain(uploaderMatches, document => document.DocumentId == privateDocument.DocumentId);
        Assert.Single(await service.SearchAsync(principal, new DocumentQueryOptions { SearchTerm = "ContosoDashboard Development" }));
        Assert.Single(await service.SearchAsync(principal, new DocumentQueryOptions { SearchTerm = "Shared handbook" }));

        var filtered = await service.SearchAsync(principal, new DocumentQueryOptions
        {
            Category = "Project Documents",
            ProjectId = 1,
            FromUtc = projectDocument.UploadedAtUtc.AddMinutes(-1),
            ThroughUtc = projectDocument.UploadedAtUtc.AddMinutes(1)
        });
        Assert.Equal(projectDocument.DocumentId, Assert.Single(filtered).DocumentId);

        var bySize = await service.SearchAsync(principal, new DocumentQueryOptions
        {
            SortBy = "size",
            SortDescending = true
        });
        Assert.Equal(new[] { projectDocument.DocumentId, ownerDocument.DocumentId, sharedPersonalDocument.DocumentId },
            bySize.Select(document => document.DocumentId));
        Assert.DoesNotContain(bySize, document => document.DocumentId == privateDocument.DocumentId);

        var byTitle = await service.SearchAsync(principal, new DocumentQueryOptions
        {
            SortBy = "title",
            SortDescending = false
        });
        Assert.Equal(new[] { "Architecture notes", "Deployment guide", "Shared handbook" },
            byTitle.Select(document => document.Title));

        var byCategory = await service.SearchAsync(principal, new DocumentQueryOptions
        {
            SortBy = "category",
            SortDescending = false
        });
        Assert.Equal(byCategory.OrderBy(document => document.Category).ThenBy(document => document.DocumentId)
            .Select(document => document.DocumentId), byCategory.Select(document => document.DocumentId));

        var byDate = await service.SearchAsync(principal, new DocumentQueryOptions
        {
            SortBy = "date",
            SortDescending = false
        });
        Assert.Equal(new[] { ownerDocument.DocumentId, projectDocument.DocumentId, sharedPersonalDocument.DocumentId },
            byDate.Select(document => document.DocumentId));
    }

    private static Document NewDocument(
        int ownerId,
        string title,
        string description,
        string fileName,
        int? projectId,
        string category,
        long size,
        DateTime uploadedAtUtc)
    {
        return new Document
        {
            Title = title,
            Description = description,
            Category = category,
            OriginalFileName = $"{fileName}.pdf",
            FilePath = $"files/{Guid.NewGuid():N}.pdf",
            FileSize = size,
            FileType = "application/pdf",
            UploadedAtUtc = uploadedAtUtc,
            UploadedByUserId = ownerId,
            ProjectId = projectId
        };
    }

    private static DocumentTag NewTag(string value)
    {
        return new DocumentTag
        {
            Value = value,
            NormalizedValue = value.ToUpperInvariant()
        };
    }
}