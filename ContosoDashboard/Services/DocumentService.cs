using System.Security;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public interface IDocumentService
{
    long MaximumFileSizeBytes { get; }
    IReadOnlyList<string> Categories { get; }
    Task<DocumentUploadResult> UploadAsync(DocumentUploadRequest request, CancellationToken cancellationToken = default);
}

public sealed class DocumentUploadRequest
{
    public int UploaderUserId { get; set; }
    public required Stream ContentStream { get; set; }
    public required string OriginalFileName { get; set; }
    public required string Title { get; set; }
    public required string Category { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public int? ProjectId { get; set; }
}

public sealed record DocumentUploadResult(bool Succeeded, string Message, Document? Document = null);

public sealed class DocumentService : IDocumentService
{
    private const long MaxFileSizeBytes = 25L * 1024 * 1024;

    private static readonly IReadOnlyList<string> UploadCategories =
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
    private readonly IFileStorageService _storage;
    private readonly IFileScanner _scanner;
    private readonly INotificationService _notifications;
    private readonly ILogger<DocumentService> _logger;

    public long MaximumFileSizeBytes => MaxFileSizeBytes;
    public IReadOnlyList<string> Categories => UploadCategories;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService storage,
        IFileScanner scanner,
        INotificationService notifications,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _storage = storage;
        _scanner = scanner;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<DocumentUploadResult> UploadAsync(DocumentUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.UploaderUserId <= 0)
        {
            return Failure("Sign in before uploading a document.");
        }

        var title = request.Title?.Trim();
        var category = request.Category?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 255)
        {
            return Failure("Enter a title of 1 to 255 characters.");
        }

        if (string.IsNullOrWhiteSpace(category) || !UploadCategories.Contains(category, StringComparer.Ordinal))
        {
            return Failure("Choose a valid document category.");
        }

        if (request.Description?.Length > 2000)
        {
            return Failure("The description cannot exceed 2,000 characters.");
        }

        if (request.ContentStream is null || !request.ContentStream.CanRead || !request.ContentStream.CanSeek)
        {
            return Failure("The selected file could not be read.");
        }

        var fileSize = request.ContentStream.Length;
        if (fileSize <= 0 || fileSize > MaxFileSizeBytes)
        {
            return Failure("Files must be larger than 0 bytes and no larger than 25 MB.");
        }

        var originalFileName = Path.GetFileName(request.OriginalFileName?.Replace('\\', '/') ?? string.Empty).Trim();
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (originalFileName.Length is 0 or > 255 || !SupportedTypes.TryGetValue(extension, out var fileType))
        {
            return Failure("This file type is not supported.");
        }

        var tags = ParseTags(request.Tags);
        if (tags.Any(tag => tag.Length > 100))
        {
            return Failure("Tags cannot exceed 100 characters.");
        }

        var uploader = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.UserId == request.UploaderUserId, cancellationToken);
        if (uploader is null)
        {
            return Failure("Your account could not be verified.");
        }

        Project? project = null;
        if (request.ProjectId is int projectId)
        {
            project = await _context.Projects
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.ProjectId == projectId, cancellationToken);
            if (project is null)
            {
                return Failure("The selected project could not be found.");
            }

            var isProjectMember = await _context.ProjectMembers
                .AnyAsync(member => member.ProjectId == projectId && member.UserId == uploader.UserId, cancellationToken);
            var isAuthorized = uploader.Role == UserRole.Administrator ||
                project.ProjectManagerId == uploader.UserId ||
                isProjectMember;
            if (!isAuthorized)
            {
                return Failure("You do not have permission to upload to this project.");
            }
        }

        FileScanResult scanResult;
        try
        {
            request.ContentStream.Position = 0;
            scanResult = await _scanner.ScanAsync(request.ContentStream, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Document scan did not produce a verdict for user {UserId}.", uploader.UserId);
            return Failure("The file could not be scanned. Try again when the malware scanner is available.");
        }

        if (scanResult.Verdict != FileScanVerdict.Clean)
        {
            var message = scanResult.Verdict == FileScanVerdict.Infected
                ? "The file was rejected because it did not pass the malware scan."
                : "The file could not be scanned. Try again when the malware scanner is available.";
            return Failure(message);
        }

        var relativePath = $"{uploader.UserId}/{request.ProjectId?.ToString() ?? "personal"}/{Guid.NewGuid():N}{extension}";
        var document = new Document
        {
            Title = title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Category = category,
            OriginalFileName = originalFileName,
            FilePath = relativePath,
            FileType = fileType,
            FileSizeBytes = fileSize,
            UploadedAtUtc = DateTime.UtcNow,
            UploaderUserId = uploader.UserId,
            ProjectId = request.ProjectId,
            Tags = tags.Select(tag => new DocumentTag { Tag = tag }).ToList()
        };

        var fileStored = false;
        try
        {
            request.ContentStream.Position = 0;
            await _storage.UploadAsync(request.ContentStream, relativePath, cancellationToken);
            fileStored = true;

            _context.Documents.Add(document);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (fileStored)
            {
                await TryDeleteStoredFileAsync(relativePath);
            }

            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Document upload failed for user {UserId}.", uploader.UserId);
            if (fileStored)
            {
                await TryDeleteStoredFileAsync(relativePath);
            }

            return Failure("The document could not be saved. No document was accepted.");
        }

        if (project is not null)
        {
            await NotifyProjectMembersAsync(project, uploader.UserId, document, cancellationToken);
        }

        return new DocumentUploadResult(true, "Document uploaded successfully.", document);
    }

    private async Task NotifyProjectMembersAsync(Project project, int uploaderUserId, Document document, CancellationToken cancellationToken)
    {
        var recipientIds = await _context.ProjectMembers
            .Where(member => member.ProjectId == project.ProjectId)
            .Select(member => member.UserId)
            .ToListAsync(cancellationToken);
        recipientIds.Add(project.ProjectManagerId);
        recipientIds = recipientIds
            .Where(userId => userId != uploaderUserId)
            .Distinct()
            .ToList();

        var recipients = await _context.Users
            .Where(user => recipientIds.Contains(user.UserId) && user.InAppNotificationsEnabled)
            .Select(user => user.UserId)
            .ToListAsync(cancellationToken);

        foreach (var recipientId in recipients)
        {
            try
            {
                await _notifications.CreateNotificationAsync(new Notification
                {
                    UserId = recipientId,
                    Title = "New project document",
                    Message = $"{document.Title} was added to {project.Name}.",
                    Type = NotificationType.DocumentUploaded,
                    Priority = NotificationPriority.Informational
                });
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not notify user {UserId} about document {DocumentId}.", recipientId, document.DocumentId);
            }
        }
    }

    private async Task TryDeleteStoredFileAsync(string relativePath)
    {
        try
        {
            await _storage.DeleteAsync(relativePath);
        }
        catch (Exception cleanupException)
        {
            _logger.LogError(cleanupException, "Could not remove uncommitted document file {RelativePath}.", relativePath);
        }
    }

    private static List<string> ParseTags(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => tag.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static DocumentUploadResult Failure(string message) => new(false, message);
}