using System.Security.Claims;
using System.Text;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public sealed record DocumentUploadRequest
{
    public string OriginalFileName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ContentType { get; init; }
    public int? ProjectId { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public Stream Content { get; init; } = Stream.Null;
}

public sealed record DocumentUploadResult(
    string OriginalFileName,
    bool Success,
    string? Error = null,
    int? DocumentId = null);

public sealed class DocumentQueryOptions
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public int? ProjectId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ThroughUtc { get; set; }
    public string? SortBy { get; set; } = "uploadedAt";
    public bool SortDescending { get; set; } = true;
    public bool OwnerOnly { get; set; }
    public bool SharedWithMeOnly { get; set; }
}

public sealed record DocumentContentAccessResult(
    bool Found,
    bool PreviewSupported,
    Stream? Content = null,
    string? ContentType = null,
    string? FileName = null);

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentUploadResult>> UploadAsync(
        ClaimsPrincipal principal,
        IReadOnlyCollection<DocumentUploadRequest> uploads,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> GetMyDocumentsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Document>> SearchAsync(
        ClaimsPrincipal principal,
        DocumentQueryOptions options,
        CancellationToken cancellationToken = default);
    Task<Document?> GetAuthorizedDocumentAsync(
        ClaimsPrincipal principal,
        int documentId,
        CancellationToken cancellationToken = default);
    Task<DocumentContentAccessResult> GetDocumentContentAsync(
        ClaimsPrincipal principal,
        int documentId,
        bool preview,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentService : IDocumentService
{
    public const long MaximumFileSizeBytes = 25L * 1024 * 1024;

    public static readonly string[] Categories =
    [
        "Project Documents",
        "Team Resources",
        "Personal Files",
        "Reports",
        "Presentations",
        "Other"
    ];

    private static readonly IReadOnlyDictionary<string, string> SupportedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IMalwareScanner _malwareScanner;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        IMalwareScanner malwareScanner)
    {
        _context = context;
        _fileStorage = fileStorage;
        _malwareScanner = malwareScanner;
    }

    public async Task<IReadOnlyList<DocumentUploadResult>> UploadAsync(
        ClaimsPrincipal principal,
        IReadOnlyCollection<DocumentUploadRequest> uploads,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DocumentUploadResult>(uploads.Count);
        foreach (var upload in uploads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await UploadOneAsync(principal, upload, cancellationToken));
        }

        return results;
    }

    public async Task<IReadOnlyList<Document>> GetMyDocumentsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        if (actor is null)
        {
            return [];
        }

        return await _context.Documents
            .AsNoTracking()
            .Include(document => document.Project)
            .Include(document => document.Uploader)
            .Include(document => document.Tags)
            .Where(document => document.UploadedByUserId == actor.UserId)
            .OrderByDescending(document => document.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> SearchAsync(
        ClaimsPrincipal principal,
        DocumentQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        if (actor is null)
        {
            return [];
        }

        var query = BuildAuthorizedQuery(actor);
        if (options.OwnerOnly)
        {
            query = query.Where(document => document.UploadedByUserId == actor.UserId);
        }

        if (options.SharedWithMeOnly)
        {
            query = query.Where(document => document.Shares.Any(share =>
                share.RecipientUserId == actor.UserId ||
                (actor.Department != null && share.RecipientDepartment == actor.Department)));
        }

        if (!string.IsNullOrWhiteSpace(options.SearchTerm))
        {
            var term = options.SearchTerm.Trim();
            var normalizedTerm = term.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
            query = query.Where(document =>
                document.Title.Contains(term) ||
                (document.Description != null && document.Description.Contains(term)) ||
                document.Tags.Any(tag => tag.Value.Contains(term) || tag.NormalizedValue.Contains(normalizedTerm)) ||
                document.Uploader.DisplayName.Contains(term) ||
                (document.Project != null && document.Project.Name.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(options.Category))
        {
            query = query.Where(document => document.Category == options.Category);
        }

        if (options.ProjectId.HasValue)
        {
            query = query.Where(document => document.ProjectId == options.ProjectId.Value);
        }

        if (options.FromUtc.HasValue)
        {
            query = query.Where(document => document.UploadedAtUtc >= options.FromUtc.Value);
        }

        if (options.ThroughUtc.HasValue)
        {
            query = query.Where(document => document.UploadedAtUtc <= options.ThroughUtc.Value);
        }

        var sortBy = options.SortBy?.Trim().ToLowerInvariant();
        IOrderedQueryable<Document> orderedQuery = sortBy switch
        {
            "title" => options.SortDescending ? query.OrderByDescending(document => document.Title) : query.OrderBy(document => document.Title),
            "category" => options.SortDescending ? query.OrderByDescending(document => document.Category) : query.OrderBy(document => document.Category),
            "size" => options.SortDescending ? query.OrderByDescending(document => document.FileSize) : query.OrderBy(document => document.FileSize),
            "date" or "uploadedat" => options.SortDescending ? query.OrderByDescending(document => document.UploadedAtUtc) : query.OrderBy(document => document.UploadedAtUtc),
            _ => query.OrderByDescending(document => document.UploadedAtUtc)
        };

        return await orderedQuery
            .ThenBy(document => document.DocumentId)
            .Include(document => document.Project)
            .Include(document => document.Uploader)
            .Include(document => document.Tags)
            .AsNoTracking()
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetAuthorizedDocumentAsync(
        ClaimsPrincipal principal,
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        if (actor is null)
        {
            return null;
        }

        return await BuildAuthorizedQuery(actor)
            .Include(document => document.Project)
            .Include(document => document.Uploader)
            .Include(document => document.Tags)
            .AsNoTracking()
            .SingleOrDefaultAsync(document => document.DocumentId == documentId, cancellationToken);
    }

    public async Task<DocumentContentAccessResult> GetDocumentContentAsync(
        ClaimsPrincipal principal,
        int documentId,
        bool preview,
        CancellationToken cancellationToken = default)
    {
        var document = await GetAuthorizedDocumentAsync(principal, documentId, cancellationToken);
        if (document is null)
        {
            return new DocumentContentAccessResult(false, false);
        }

        var previewSupported = document.FileType is "application/pdf" or "image/jpeg" or "image/png";
        if (preview && !previewSupported)
        {
            return new DocumentContentAccessResult(true, false);
        }

        var content = await _fileStorage.OpenReadAsync(document.FilePath, cancellationToken);
        if (content is null)
        {
            return new DocumentContentAccessResult(false, false);
        }

        return new DocumentContentAccessResult(true, previewSupported, content, document.FileType, document.OriginalFileName);
    }

    private async Task<DocumentUploadResult> UploadOneAsync(
        ClaimsPrincipal principal,
        DocumentUploadRequest upload,
        CancellationToken cancellationToken)
    {
        if (!principal.Identity?.IsAuthenticated ?? true)
        {
            return Failed(upload, "Sign in before uploading documents.");
        }

        if (!TryGetUserId(principal, out var userId))
        {
            return Failed(upload, "The signed-in user could not be identified.");
        }

        var actor = await _context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.UserId == userId, cancellationToken);
        if (actor is null)
        {
            return Failed(upload, "The signed-in user is no longer available.");
        }

        var validationError = await ValidateAsync(upload, actor, cancellationToken);
        if (validationError is not null)
        {
            return Failed(upload, validationError);
        }

        StagedFile? stagedFile = null;
        string? publishedKey = null;
        try
        {
            stagedFile = await _fileStorage.StageAsync(upload.Content, MaximumFileSizeBytes, cancellationToken);
            var fileSize = stagedFile.Length;
            var scanResult = await _malwareScanner.ScanAsync(stagedFile.PhysicalPath, cancellationToken);
            if (scanResult.Status != MalwareScanStatus.Clean)
            {
                return Failed(upload, scanResult.Status == MalwareScanStatus.ThreatDetected
                    ? "The file was rejected because malware was detected."
                    : "The file could not be cleared by the malware scanner. Check the local scanner and try again.");
            }

            var extension = Path.GetExtension(Path.GetFileName(upload.OriginalFileName));
            publishedKey = await _fileStorage.PublishAsync(stagedFile, extension, cancellationToken);
            stagedFile = null;

            var document = new Document
            {
                Title = upload.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(upload.Description) ? null : upload.Description.Trim(),
                Category = upload.Category,
                OriginalFileName = Path.GetFileName(upload.OriginalFileName),
                FilePath = publishedKey,
                FileSize = fileSize,
                FileType = SupportedTypes[extension],
                UploadedAtUtc = DateTime.UtcNow,
                UploadedByUserId = actor.UserId,
                ProjectId = upload.ProjectId
            };

            foreach (var tag in NormalizeTags(upload.Tags))
            {
                document.Tags.Add(tag);
            }

            _context.Documents.Add(document);
            await _context.SaveChangesAsync(cancellationToken);
            publishedKey = null;
            return new DocumentUploadResult(upload.OriginalFileName, true, DocumentId: document.DocumentId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DocumentFileTooLargeException)
        {
            return Failed(upload, "The file may not exceed 25 MB.");
        }
        catch (Exception)
        {
            _context.ChangeTracker.Clear();
            return Failed(upload, "The upload could not be completed. Check local storage and scanner configuration, then try again.");
        }
        finally
        {
            if (stagedFile is not null)
            {
                await _fileStorage.DeleteAsync(stagedFile.Key, CancellationToken.None);
            }

            if (publishedKey is not null)
            {
                await _fileStorage.DeleteAsync(publishedKey, CancellationToken.None);
            }
        }
    }

    private async Task<string?> ValidateAsync(
        DocumentUploadRequest upload,
        User actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(upload.Title) || upload.Title.Trim().Length > 255)
        {
            return "Enter a title between 1 and 255 characters.";
        }

        if (!Categories.Contains(upload.Category, StringComparer.Ordinal))
        {
            return "Select one of the available document categories.";
        }

        var fileName = Path.GetFileName(upload.OriginalFileName);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
        {
            return "The file name is invalid or too long.";
        }

        var extension = Path.GetExtension(fileName);
        if (!SupportedTypes.ContainsKey(extension))
        {
            return "This file type is not supported.";
        }

        if (upload.Content.CanSeek && upload.Content.Length > MaximumFileSizeBytes)
        {
            return "The file may not exceed 25 MB.";
        }

        if (!string.IsNullOrWhiteSpace(upload.Description) && upload.Description.Trim().Length > 2000)
        {
            return "The description may not exceed 2,000 characters.";
        }

        if (upload.Tags.Any(tag => tag.Trim().Length > 100))
        {
            return "Each tag may not exceed 100 characters.";
        }

        if (upload.Category == "Personal Files" && upload.ProjectId.HasValue)
        {
            return "Personal Files cannot be associated with a project.";
        }

        if (upload.ProjectId.HasValue)
        {
            var canUseProject = await _context.Projects.AnyAsync(project =>
                project.ProjectId == upload.ProjectId.Value &&
                (project.ProjectManagerId == actor.UserId ||
                 project.ProjectMembers.Any(member => member.UserId == actor.UserId) ||
                 actor.Role == UserRole.Administrator), cancellationToken);
            if (!canUseProject)
            {
                return "You do not have access to the selected project.";
            }
        }

        return null;
    }

    private async Task<User?> GetActorAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true || !TryGetUserId(principal, out var userId))
        {
            return null;
        }

        return await _context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.UserId == userId, cancellationToken);
    }

    private IQueryable<Document> BuildAuthorizedQuery(User actor)
    {
        return _context.Documents.Where(document =>
            actor.Role == UserRole.Administrator ||
            document.UploadedByUserId == actor.UserId ||
            document.Shares.Any(share =>
                share.RecipientUserId == actor.UserId ||
                (actor.Department != null && share.RecipientDepartment == actor.Department)) ||
            (document.Category != "Personal Files" &&
             ((document.ProjectId.HasValue &&
               (document.Project!.ProjectManagerId == actor.UserId ||
                document.Project.ProjectMembers.Any(member => member.UserId == actor.UserId))) ||
              (actor.Role == UserRole.TeamLead && actor.Department != null && document.Uploader.Department == actor.Department))));
    }

    private static IEnumerable<DocumentTag> NormalizeTags(IEnumerable<string> tags)
    {
        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => new DocumentTag
            {
                Value = value,
                NormalizedValue = value.Normalize(NormalizationForm.FormKC).ToUpperInvariant()
            });
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out int userId)
    {
        return int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;
    }

    private static DocumentUploadResult Failed(DocumentUploadRequest upload, string error)
    {
        return new DocumentUploadResult(upload.OriginalFileName, false, error);
    }
}