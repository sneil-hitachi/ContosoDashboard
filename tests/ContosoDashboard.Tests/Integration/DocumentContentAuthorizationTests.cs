using System.Security.Claims;
using ContosoDashboard.Data;
using ContosoDashboard.Controllers;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

public sealed class DocumentContentAuthorizationTests
{
    [Fact]
    public async Task AuthorizedPreviewAndDownloadStreamOnlySupportedAcceptedContent()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var temporaryStorage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(temporaryStorage.RootPath), new FakeMalwareScanner());
        var principal = TestPrincipalFactory.Create(userId: 4);
        var pdf = await UploadAsync(service, principal, "preview.pdf");
        var image = await UploadAsync(service, principal, "preview.png");
        var word = await UploadAsync(service, principal, "download.docx");

        var preview = await CreateController(service, principal).Preview(pdf, CancellationToken.None);
        var imagePreview = await CreateController(service, principal).Preview(image, CancellationToken.None);
        var download = await CreateController(service, principal).Download(word, CancellationToken.None);
        var unsupportedPreview = await CreateController(service, principal).Preview(word, CancellationToken.None);

        var previewFile = Assert.IsType<FileStreamResult>(preview);
        var imagePreviewFile = Assert.IsType<FileStreamResult>(imagePreview);
        var downloadFile = Assert.IsType<FileStreamResult>(download);
        Assert.Equal("application/pdf", previewFile.ContentType);
        Assert.Equal("image/png", imagePreviewFile.ContentType);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", downloadFile.ContentType);
        Assert.IsType<BadRequestResult>(unsupportedPreview);
        await previewFile.FileStream.DisposeAsync();
        await imagePreviewFile.FileStream.DisposeAsync();
        await downloadFile.FileStream.DisposeAsync();
    }

    [Fact]
    public async Task AnonymousInaccessibleAndUnknownIdsDoNotReturnContent()
    {
        await using var fixture = new SqlServerLocalDbFixture();
        await fixture.InitializeAsync();
        await using var context = fixture.CreateContext();
        await using var temporaryStorage = new TemporaryFileStorage();
        var service = new DocumentService(context, new LocalFileStorageService(temporaryStorage.RootPath), new FakeMalwareScanner());
        var owner = TestPrincipalFactory.Create(userId: 4);
        var documentId = await UploadAsync(service, owner, "private.pdf", "Personal Files");
        var outsider = TestPrincipalFactory.Create(userId: 3);
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var inaccessible = await CreateController(service, outsider).Download(documentId, CancellationToken.None);
        var missing = await CreateController(service, outsider).Download(int.MaxValue, CancellationToken.None);
        var unauthenticated = await CreateController(service, anonymous).Download(documentId, CancellationToken.None);
        var invalidId = await CreateController(service, outsider).Download(0, CancellationToken.None);

        Assert.IsType<NotFoundResult>(inaccessible);
        Assert.IsType<NotFoundResult>(missing);
        Assert.IsType<UnauthorizedResult>(unauthenticated);
        Assert.IsType<BadRequestResult>(invalidId);
    }

    private static async Task<int> UploadAsync(DocumentService service, ClaimsPrincipal principal, string fileName, string category = "Reports")
    {
        var result = (await service.UploadAsync(principal,
        [
            new DocumentUploadRequest
            {
                OriginalFileName = fileName,
                Title = Path.GetFileNameWithoutExtension(fileName),
                Category = category,
                Content = new MemoryStream([1, 2, 3])
            }
        ])).Single();

        Assert.True(result.Success, result.Error);
        return result.DocumentId!.Value;
    }

    private static DocumentsController CreateController(IDocumentService service, ClaimsPrincipal principal)
    {
        var controller = new DocumentsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
        return controller;
    }
}