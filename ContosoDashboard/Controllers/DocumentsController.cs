using System.Security.Claims;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContosoDashboard.Controllers;

[ApiController]
[Route("documents")]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet("{documentId:int}/download")]
    public async Task<IActionResult> Download(int documentId, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        if (documentId <= 0)
        {
            return BadRequest();
        }

        var result = await _documentService.GetDocumentContentAsync(User, documentId, preview: false, cancellationToken);
        if (!result.Found || result.Content is null)
        {
            return NotFound();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "no-store";
        return File(result.Content, result.ContentType!, Path.GetFileName(result.FileName), enableRangeProcessing: true);
    }

    [HttpGet("{documentId:int}/preview")]
    public async Task<IActionResult> Preview(int documentId, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        if (documentId <= 0)
        {
            return BadRequest();
        }

        var result = await _documentService.GetDocumentContentAsync(User, documentId, preview: true, cancellationToken);
        if (!result.Found)
        {
            return NotFound();
        }

        if (!result.PreviewSupported || result.Content is null)
        {
            return BadRequest();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(Path.GetFileName(result.FileName ?? "document"))}";
        return File(result.Content, result.ContentType!);
    }
}