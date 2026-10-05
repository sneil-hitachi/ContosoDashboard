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
    public int? TaskId { get; init; }
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

public sealed record DocumentMetadataUpdateRequest(
    string Title,
    string Category,
    string? Description,
    int? ProjectId,
    IReadOnlyCollection<string> Tags);

public sealed record DocumentManagementResult(bool Success, string? Error = null);

public sealed record DocumentShareGrantRequest(int? RecipientUserId = null, string? RecipientDepartment = null);

public sealed record DocumentShareResult(bool Success, string? Error = null, int? ShareId = null);

public sealed record DocumentFileTypeReport(string FileType, int Count);
public sealed record DocumentUploaderReport(int UserId, string UserName, int UploadCount);
public sealed record DocumentActionReport(string Action, int Count);
public sealed record DocumentReportSummary(
    IReadOnlyList<DocumentFileTypeReport> FileTypes,
    IReadOnlyList<DocumentUploaderReport> Uploaders,
    IReadOnlyList<DocumentActionReport> AccessPatterns);

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
    Task<DocumentManagementResult> UpdateMetadataAsync(
        ClaimsPrincipal principal,
        int documentId,
        DocumentMetadataUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task<DocumentManagementResult> ReplaceFileAsync(
        ClaimsPrincipal principal,
        int documentId,
        DocumentUploadRequest replacement,
        CancellationToken cancellationToken = default);
    Task<DocumentShareResult> CreateShareAsync(
        ClaimsPrincipal principal,
        int documentId,
        DocumentShareGrantRequest request,
        CancellationToken cancellationToken = default);
    Task<bool> RevokeShareAsync(
        ClaimsPrincipal principal,
        int documentId,
        int shareId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentShare>> GetDocumentSharesAsync(
        ClaimsPrincipal principal,
        int documentId,
        CancellationToken cancellationToken = default);
    Task<DocumentManagementResult> DeleteAsync(
        ClaimsPrincipal principal,
        int documentId,
        bool confirmed,
        CancellationToken cancellationToken = default);
    Task<bool> AttachToTaskAsync(
        ClaimsPrincipal principal,
        int taskId,
        int documentId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Document>> GetTaskDocumentsAsync(
        ClaimsPrincipal principal,
        int taskId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentActivity>> GetActivityAsync(
        ClaimsPrincipal principal,
        int take = 100,
        CancellationToken cancellationToken = default);
    Task<DocumentReportSummary?> GetReportAsync(
        ClaimsPrincipal principal,
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
    private readonly INotificationService? _notificationService;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        IMalwareScanner malwareScanner,
        INotificationService? notificationService = null)
    {
        _context = context;
        _fileStorage = fileStorage;
        _malwareScanner = malwareScanner;
        _notificationService = notificationService;
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

        if (!preview)
        {
            var actor = await GetActorAsync(principal, cancellationToken);
            if (actor is not null)
            {
                await RecordActivityAsync(document, actor, "Downloaded", cancellationToken);
            }
        }

        return new DocumentContentAccessResult(true, previewSupported, content, document.FileType, document.OriginalFileName);
    }

    public async Task<DocumentManagementResult> UpdateMetadataAsync(
        ClaimsPrincipal principal,
        int documentId,
        DocumentMetadataUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        var document = await _context.Documents
            .Include(item => item.Project)
            .Include(item => item.Uploader)
            .Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (actor is null || document is null || !CanManageDocument(actor, document))
        {
            return new DocumentManagementResult(false, "You are not authorized to manage this document.");
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 255 ||
            !Categories.Contains(request.Category, StringComparer.Ordinal) ||
            (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 2000) ||
            request.Tags.Any(tag => tag.Trim().Length > 100) ||
            (request.Category == "Personal Files" &&
             (request.ProjectId.HasValue || await _context.TaskDocuments.AnyAsync(link => link.DocumentId == documentId, cancellationToken))))
        {
            return new DocumentManagementResult(false, "One or more document details are invalid.");
        }

        if (request.ProjectId.HasValue)
        {
            var canUseProject = await _context.Projects.AnyAsync(project =>
                project.ProjectId == request.ProjectId.Value &&
                (project.ProjectManagerId == actor.UserId ||
                 project.ProjectMembers.Any(member => member.UserId == actor.UserId) ||
                 actor.Role == UserRole.Administrator), cancellationToken);
            if (!canUseProject)
            {
                return new DocumentManagementResult(false, "You do not have access to the selected project.");
            }
        }

        document.Title = request.Title.Trim();
        document.Category = request.Category;
        document.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        document.ProjectId = request.ProjectId;
        _context.DocumentTags.RemoveRange(document.Tags);
        foreach (var tag in NormalizeTags(request.Tags))
        {
            document.Tags.Add(tag);
        }

        _context.DocumentActivities.Add(CreateActivity(document.DocumentId, document.Title, actor, "MetadataUpdated"));
        await _context.SaveChangesAsync(cancellationToken);
        return new DocumentManagementResult(true);
    }

    public async Task<DocumentManagementResult> ReplaceFileAsync(
        ClaimsPrincipal principal,
        int documentId,
        DocumentUploadRequest replacement,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        var document = await _context.Documents
            .Include(item => item.Project)
            .Include(item => item.Uploader)
            .SingleOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (actor is null || document is null || !CanManageDocument(actor, document))
        {
            return new DocumentManagementResult(false, "You are not authorized to manage this document.");
        }

        var validationRequest = replacement with
        {
            Title = document.Title,
            Category = document.Category,
            ProjectId = null
        };
        var validationError = await ValidateAsync(validationRequest, actor, cancellationToken);
        if (validationError is not null)
        {
            return new DocumentManagementResult(false, validationError);
        }

        StagedFile? stagedFile = null;
        string? replacementKey = null;
        var previousKey = document.FilePath;
        try
        {
            stagedFile = await _fileStorage.StageAsync(replacement.Content, MaximumFileSizeBytes, cancellationToken);
            var scanResult = await _malwareScanner.ScanAsync(stagedFile.PhysicalPath, cancellationToken);
            if (scanResult.Status != MalwareScanStatus.Clean)
            {
                return new DocumentManagementResult(false, scanResult.Status == MalwareScanStatus.ThreatDetected
                    ? "The replacement was rejected because malware was detected."
                    : "The replacement could not be cleared by the malware scanner.");
            }

            var extension = Path.GetExtension(Path.GetFileName(replacement.OriginalFileName));
            var replacementSize = stagedFile.Length;
            replacementKey = await _fileStorage.PublishAsync(stagedFile, extension, cancellationToken);
            stagedFile = null;
            document.FilePath = replacementKey;
            document.FileSize = replacementSize;
            document.FileType = SupportedTypes[extension];
            document.OriginalFileName = Path.GetFileName(replacement.OriginalFileName);
            _context.DocumentActivities.Add(CreateActivity(document.DocumentId, document.Title, actor, "Replaced"));
            await _context.SaveChangesAsync(cancellationToken);
            replacementKey = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _context.ChangeTracker.Clear();
            return new DocumentManagementResult(false, "The replacement could not be completed. The existing file was retained.");
        }
        finally
        {
            if (stagedFile is not null)
            {
                await _fileStorage.DeleteAsync(stagedFile.Key, CancellationToken.None);
            }

            if (replacementKey is not null)
            {
                await _fileStorage.DeleteAsync(replacementKey, CancellationToken.None);
            }
        }

        await _fileStorage.DeleteAsync(previousKey, CancellationToken.None);
        return new DocumentManagementResult(true);
    }

    public async Task<DocumentShareResult> CreateShareAsync(
        ClaimsPrincipal principal,
        int documentId,
        DocumentShareGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        var document = await _context.Documents
            .SingleOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (actor is null || document is null || document.UploadedByUserId != actor.UserId)
        {
            return new DocumentShareResult(false, "Only the document owner can manage shares.");
        }

        var department = request.RecipientDepartment?.Trim();
        if ((request.RecipientUserId.HasValue == !string.IsNullOrWhiteSpace(department)) ||
            (department?.Length > 100))
        {
            return new DocumentShareResult(false, "Select exactly one valid user or department recipient.");
        }

        if (request.RecipientUserId.HasValue &&
            !await _context.Users.AnyAsync(user => user.UserId == request.RecipientUserId.Value, cancellationToken))
        {
            return new DocumentShareResult(false, "The selected recipient is unavailable.");
        }

        var duplicate = await _context.DocumentShares.AnyAsync(share =>
            share.DocumentId == documentId &&
            (request.RecipientUserId.HasValue
                ? share.RecipientUserId == request.RecipientUserId.Value
                : share.RecipientDepartment == department), cancellationToken);
        if (duplicate)
        {
            return new DocumentShareResult(false, "This recipient already has access.");
        }

        var share = new DocumentShare
        {
            DocumentId = documentId,
            GrantedByUserId = actor.UserId,
            RecipientUserId = request.RecipientUserId,
            RecipientDepartment = request.RecipientUserId.HasValue ? null : department,
            GrantedAtUtc = DateTime.UtcNow
        };
        _context.DocumentShares.Add(share);
        _context.DocumentActivities.Add(CreateActivity(document.DocumentId, document.Title, actor, "Shared"));
        await _context.SaveChangesAsync(cancellationToken);

        if (_notificationService is not null)
        {
            var recipients = await _context.Users
                .Where(user => user.InAppNotificationsEnabled &&
                    (request.RecipientUserId.HasValue
                        ? user.UserId == request.RecipientUserId.Value
                        : user.Department == department))
                .Select(user => user.UserId)
                .ToListAsync(cancellationToken);
            foreach (var recipientId in recipients)
            {
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = recipientId,
                    Title = "A document was shared with you",
                    Message = $"{actor.DisplayName} shared '{document.Title}' with you.",
                    Type = NotificationType.DocumentShared,
                    Priority = NotificationPriority.Informational
                });
            }
        }

        return new DocumentShareResult(true, ShareId: share.DocumentShareId);
    }

    public async Task<bool> RevokeShareAsync(
        ClaimsPrincipal principal,
        int documentId,
        int shareId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        var share = await _context.DocumentShares
            .Include(item => item.Document)
            .SingleOrDefaultAsync(item => item.DocumentShareId == shareId && item.DocumentId == documentId, cancellationToken);
        if (actor is null || share?.Document.UploadedByUserId != actor.UserId)
        {
            return false;
        }

        _context.DocumentActivities.Add(CreateActivity(share.DocumentId, share.Document.Title, actor, "ShareRevoked"));
        _context.DocumentShares.Remove(share);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<DocumentShare>> GetDocumentSharesAsync(
        ClaimsPrincipal principal,
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        if (actor is null || !await _context.Documents.AnyAsync(document =>
                document.DocumentId == documentId && document.UploadedByUserId == actor.UserId, cancellationToken))
        {
            return [];
        }

        return await _context.DocumentShares
            .Where(share => share.DocumentId == documentId)
            .OrderBy(share => share.GrantedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentManagementResult> DeleteAsync(
        ClaimsPrincipal principal,
        int documentId,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return new DocumentManagementResult(false, "Confirm deletion before removing this document.");
        }

        var actor = await GetActorAsync(principal, cancellationToken);
        var document = await _context.Documents
            .Include(item => item.Project)
            .Include(item => item.Uploader)
            .SingleOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (actor is null || document is null || !CanManageDocument(actor, document))
        {
            return new DocumentManagementResult(false, "You are not authorized to delete this document.");
        }

        var filePath = document.FilePath;
        _context.DocumentActivities.Add(CreateActivity(document.DocumentId, document.Title, actor, "Deleted"));
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync(cancellationToken);
        await _fileStorage.DeleteAsync(filePath, cancellationToken);
        return new DocumentManagementResult(true);
    }

    public async Task<IReadOnlyList<DocumentActivity>> GetActivityAsync(
        ClaimsPrincipal principal,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        if (actor?.Role != UserRole.Administrator)
        {
            return [];
        }

        return await _context.DocumentActivities
            .AsNoTracking()
            .OrderByDescending(activity => activity.OccurredAtUtc)
            .ThenByDescending(activity => activity.DocumentActivityId)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentReportSummary?> GetReportAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        if (actor?.Role != UserRole.Administrator)
        {
            return null;
        }

        var fileTypeRows = await _context.Documents
            .GroupBy(document => document.FileType)
            .Select(group => new { FileType = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToListAsync(cancellationToken);
        var uploaderRows = await _context.Documents
            .GroupBy(document => document.UploadedByUserId)
            .Select(group => new { UserId = group.Key, UploadCount = group.Count() })
            .Join(_context.Users, item => item.UserId, user => user.UserId,
                (item, user) => new { user.UserId, UserName = user.DisplayName, item.UploadCount })
            .OrderByDescending(item => item.UploadCount)
            .ToListAsync(cancellationToken);
        var actionRows = await _context.DocumentActivities
            .GroupBy(activity => activity.Action)
            .Select(group => new { Action = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ToListAsync(cancellationToken);

        return new DocumentReportSummary(
            fileTypeRows.Select(item => new DocumentFileTypeReport(item.FileType, item.Count)).ToList(),
            uploaderRows.Select(item => new DocumentUploaderReport(item.UserId, item.UserName, item.UploadCount)).ToList(),
            actionRows.Select(item => new DocumentActionReport(item.Action, item.Count)).ToList());
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

        var task = upload.TaskId.HasValue
            ? await GetAuthorizedTaskAsync(actor, upload.TaskId.Value, cancellationToken)
            : null;

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
                ProjectId = task?.ProjectId ?? upload.ProjectId
            };

            foreach (var tag in NormalizeTags(upload.Tags))
            {
                document.Tags.Add(tag);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            _context.Documents.Add(document);
            if (task is not null)
            {
                _context.TaskDocuments.Add(new TaskDocument
                {
                    Task = task,
                    Document = document,
                    AttachedByUserId = actor.UserId,
                    AttachedAtUtc = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync(cancellationToken);
            await RecordActivityAsync(document, actor, "Uploaded", cancellationToken);
            if (document.ProjectId.HasValue)
            {
                await NotifyProjectMembersAsync(document, actor, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
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

    public async Task<bool> AttachToTaskAsync(
        ClaimsPrincipal principal,
        int taskId,
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        if (actor is null)
        {
            return false;
        }

        var task = await GetAuthorizedTaskAsync(actor, taskId, cancellationToken);
        if (task is null)
        {
            return false;
        }

        var document = await BuildAuthorizedQuery(actor)
            .SingleOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (document is null || document.ProjectId != task.ProjectId ||
            await _context.TaskDocuments.AnyAsync(attachment => attachment.TaskId == taskId && attachment.DocumentId == documentId, cancellationToken))
        {
            return false;
        }

        _context.TaskDocuments.Add(new TaskDocument
        {
            TaskId = taskId,
            DocumentId = documentId,
            AttachedByUserId = actor.UserId,
            AttachedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Document>> GetTaskDocumentsAsync(
        ClaimsPrincipal principal,
        int taskId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(principal, cancellationToken);
        if (actor is null || await GetAuthorizedTaskAsync(actor, taskId, cancellationToken) is null)
        {
            return [];
        }

        return await BuildAuthorizedQuery(actor)
            .Where(document => document.TaskDocuments.Any(attachment => attachment.TaskId == taskId))
            .Include(document => document.Project)
            .Include(document => document.Uploader)
            .AsNoTracking()
            .OrderBy(document => document.Title)
            .ToListAsync(cancellationToken);
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

        if (upload.Category == "Personal Files" && (upload.ProjectId.HasValue || upload.TaskId.HasValue))
        {
            return "Personal Files cannot be associated with a project or task.";
        }

        TaskItem? task = null;
        if (upload.TaskId.HasValue)
        {
            task = await GetAuthorizedTaskAsync(actor, upload.TaskId.Value, cancellationToken);
            if (task is null || (upload.ProjectId.HasValue && upload.ProjectId != task.ProjectId))
            {
                return "You do not have access to the selected task or its project does not match.";
            }
        }

        var projectId = task?.ProjectId ?? upload.ProjectId;
        if (projectId.HasValue)
        {
            var canUseProject = await _context.Projects.AnyAsync(project =>
                project.ProjectId == projectId.Value &&
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

    private async Task<TaskItem?> GetAuthorizedTaskAsync(User actor, int taskId, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .Include(item => item.Project)
            .ThenInclude(project => project!.ProjectMembers)
            .SingleOrDefaultAsync(item => item.TaskId == taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var allowed = actor.Role == UserRole.Administrator ||
                      task.AssignedUserId == actor.UserId ||
                      task.CreatedByUserId == actor.UserId ||
                      task.Project?.ProjectManagerId == actor.UserId ||
                      task.Project?.ProjectMembers.Any(member => member.UserId == actor.UserId) == true;
        return allowed ? task : null;
    }

    private async Task NotifyProjectMembersAsync(Document document, User actor, CancellationToken cancellationToken)
    {
        if (_notificationService is null || !document.ProjectId.HasValue)
        {
            return;
        }

        var recipients = await _context.ProjectMembers
            .Where(member => member.ProjectId == document.ProjectId.Value && member.UserId != actor.UserId)
            .Select(member => member.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var recipientId in recipients)
        {
            await _notificationService.CreateNotificationAsync(new Notification
            {
                UserId = recipientId,
                Title = "New project document",
                Message = $"{actor.DisplayName} uploaded '{document.Title}'.",
                Type = NotificationType.ProjectUpdate,
                Priority = NotificationPriority.Informational
            });
        }
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

    private static bool CanManageDocument(User actor, Document document)
    {
        return actor.Role == UserRole.Administrator ||
               actor.UserId == document.UploadedByUserId ||
               (document.Category != "Personal Files" &&
                ((document.Project?.ProjectManagerId == actor.UserId) ||
                 (actor.Role == UserRole.TeamLead && actor.Department != null && document.Uploader.Department == actor.Department)));
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

    private async Task RecordActivityAsync(Document document, User actor, string action, CancellationToken cancellationToken)
    {
        _context.DocumentActivities.Add(CreateActivity(document.DocumentId, document.Title, actor, action));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static DocumentActivity CreateActivity(int? documentId, string title, User actor, string action)
    {
        return new DocumentActivity
        {
            DocumentId = documentId,
            DocumentTitleSnapshot = title,
            ActorUserId = actor.UserId,
            ActorNameSnapshot = actor.DisplayName,
            Action = action,
            OccurredAtUtc = DateTime.UtcNow
        };
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