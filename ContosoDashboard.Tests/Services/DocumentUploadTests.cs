using System.Data.Common;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class DocumentUploadTests
{
    [Fact]
    public async Task Upload_persists_metadata_tags_and_bytes_using_a_guid_key()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var content = new MemoryStream("confidential bytes"u8.ToArray());
        var storage = new FakeStorage();
        var scanner = new FakeScanner(FileScanVerdict.Clean);
        var notifications = new FakeNotifications();
        var service = CreateService(database.Context, storage, scanner, notifications);

        var result = await service.UploadAsync(CreateRequest(content));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Document);
        Assert.Equal(4, result.Document.UploaderUserId);
        Assert.Equal("Reports", result.Document.Category);
        Assert.Equal("application/pdf", result.Document.FileType);
        Assert.Equal(content.Length, result.Document.FileSizeBytes);
        Assert.Equal(new[] { "finance", "fy2026" }, result.Document.Tags.Select(tag => tag.Tag));
        Assert.StartsWith("4/personal/", result.Document.FilePath);
        Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(result.Document.FilePath), "N", out _));
        Assert.Equal(content.ToArray(), storage.Files[result.Document.FilePath]);
        Assert.Equal(1, scanner.ScanCount);
        Assert.Empty(notifications.Created);
    }

    [Theory]
    [InlineData("", "Reports")]
    [InlineData("Quarterly plan", "")]
    [InlineData("Quarterly plan", "Unlisted category")]
    public async Task Upload_rejects_missing_or_invalid_required_metadata(string title, string category)
    {
        await using var database = await TestDatabase.CreateAsync();
        using var content = new MemoryStream("content"u8.ToArray());
        var storage = new FakeStorage();
        var scanner = new FakeScanner(FileScanVerdict.Clean);
        var service = CreateService(database.Context, storage, scanner, new FakeNotifications());
        var request = CreateRequest(content);
        request.Title = title;
        request.Category = category;

        var result = await service.UploadAsync(request);

        Assert.False(result.Succeeded);
        Assert.Empty(storage.Files);
        Assert.Equal(0, scanner.ScanCount);
    }

    [Fact]
    public async Task Upload_rejects_unsupported_extensions_before_scanning()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var content = new MemoryStream("content"u8.ToArray());
        var storage = new FakeStorage();
        var scanner = new FakeScanner(FileScanVerdict.Clean);
        var service = CreateService(database.Context, storage, scanner, new FakeNotifications());
        var request = CreateRequest(content);
        request.OriginalFileName = "payload.exe";

        var result = await service.UploadAsync(request);

        Assert.False(result.Succeeded);
        Assert.Empty(storage.Files);
        Assert.Equal(0, scanner.ScanCount);
    }

    [Fact]
    public async Task Upload_rejects_files_larger_than_25_mib_before_scanning()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var content = new MemoryStream();
        content.SetLength(25L * 1024 * 1024 + 1);
        var storage = new FakeStorage();
        var scanner = new FakeScanner(FileScanVerdict.Clean);
        var service = CreateService(database.Context, storage, scanner, new FakeNotifications());

        var result = await service.UploadAsync(CreateRequest(content));

        Assert.False(result.Succeeded);
        Assert.Empty(storage.Files);
        Assert.Equal(0, scanner.ScanCount);
    }

    [Theory]
    [InlineData(FileScanVerdict.Infected)]
    [InlineData(FileScanVerdict.Unavailable)]
    public async Task Upload_fails_closed_when_scanning_does_not_return_clean(FileScanVerdict verdict)
    {
        await using var database = await TestDatabase.CreateAsync();
        using var content = new MemoryStream("content"u8.ToArray());
        var storage = new FakeStorage();
        var scanner = new FakeScanner(verdict);
        var service = CreateService(database.Context, storage, scanner, new FakeNotifications());

        var result = await service.UploadAsync(CreateRequest(content));

        Assert.False(result.Succeeded);
        Assert.Empty(storage.Files);
        Assert.Equal(0, await database.Context.Documents.CountAsync());
        Assert.Equal(1, scanner.ScanCount);
    }

    [Fact]
    public async Task Upload_rejects_project_upload_by_a_non_member()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Users.Add(new User
        {
            UserId = 5,
            Email = "outsider@contoso.com",
            DisplayName = "Outsider",
            Department = "Sales",
            Role = UserRole.Employee
        });
        await database.Context.SaveChangesAsync();
        using var content = new MemoryStream("content"u8.ToArray());
        var storage = new FakeStorage();
        var scanner = new FakeScanner(FileScanVerdict.Clean);
        var request = CreateRequest(content);
        request.UploaderUserId = 5;
        request.ProjectId = 1;
        var service = CreateService(database.Context, storage, scanner, new FakeNotifications());

        var result = await service.UploadAsync(request);

        Assert.False(result.Succeeded);
        Assert.Empty(storage.Files);
        Assert.Equal(0, scanner.ScanCount);
    }

    [Fact]
    public async Task Upload_notifies_project_members_only_after_metadata_is_committed()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var content = new MemoryStream("content"u8.ToArray());
        var storage = new FakeStorage();
        var scanner = new FakeScanner(FileScanVerdict.Clean);
        var notifications = new FakeNotifications();
        var request = CreateRequest(content);
        request.ProjectId = 1;
        var service = CreateService(database.Context, storage, scanner, notifications);

        var result = await service.UploadAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { 2, 3 }, notifications.Created.Select(notification => notification.UserId).Order());
        Assert.All(notifications.Created, notification => Assert.Equal(NotificationType.DocumentUploaded, notification.Type));
    }

    [Fact]
    public async Task Upload_removes_stored_file_when_database_save_fails()
    {
        await using var database = await TestDatabase.CreateAsync(new FailDocumentSaveInterceptor());
        using var content = new MemoryStream("content"u8.ToArray());
        var storage = new FakeStorage();
        var notifications = new FakeNotifications();
        var service = CreateService(database.Context, storage, new FakeScanner(FileScanVerdict.Clean), notifications);

        var result = await service.UploadAsync(CreateRequest(content));

        Assert.False(result.Succeeded);
        Assert.Empty(storage.Files);
        Assert.Single(storage.DeletedPaths);
        Assert.Empty(notifications.Created);
        Assert.Equal(0, await database.Context.Documents.CountAsync());
    }

    [Fact]
    public async Task Upload_returns_failure_when_storage_cannot_save()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var content = new MemoryStream("content"u8.ToArray());
        var storage = new FakeStorage { FailUpload = true };
        var service = CreateService(database.Context, storage, new FakeScanner(FileScanVerdict.Clean), new FakeNotifications());

        var result = await service.UploadAsync(CreateRequest(content));

        Assert.False(result.Succeeded);
        Assert.Equal(0, await database.Context.Documents.CountAsync());
        Assert.Empty(storage.Files);
    }

    private static DocumentService CreateService(
        ApplicationDbContext context,
        FakeStorage storage,
        FakeScanner scanner,
        FakeNotifications notifications) =>
        new(context, storage, scanner, notifications, NullLogger<DocumentService>.Instance);

    private static DocumentUploadRequest CreateRequest(Stream content) => new()
    {
        UploaderUserId = 4,
        ContentStream = content,
        OriginalFileName = "quarterly-plan.pdf",
        Title = "Quarterly plan",
        Category = "Reports",
        Description = "Internal report",
        Tags = "Finance, FY2026"
    };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync(IInterceptor? interceptor = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection);
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }

            var context = new ApplicationDbContext(options.Options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class FakeStorage : IFileStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public List<string> DeletedPaths { get; } = [];
        public bool FailUpload { get; init; }

        public async Task UploadAsync(Stream fileStream, string relativePath, CancellationToken cancellationToken = default)
        {
            if (FailUpload)
            {
                throw new IOException("Storage failure");
            }

            using var buffer = new MemoryStream();
            await fileStream.CopyToAsync(buffer, cancellationToken);
            Files.Add(relativePath, buffer.ToArray());
        }

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            DeletedPaths.Add(relativePath);
            Files.Remove(relativePath);
            return Task.CompletedTask;
        }

        public Task<Stream> DownloadAsync(string relativePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(Files[relativePath], writable: false));

        public string GetAuthorizedUrl(int documentId) => $"/documents/{documentId}/download";
    }

    private sealed class FakeScanner(FileScanVerdict verdict) : IFileScanner
    {
        public int ScanCount { get; private set; }

        public Task<FileScanResult> ScanAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            ScanCount++;
            return Task.FromResult(new FileScanResult(verdict));
        }
    }

    private sealed class FakeNotifications : INotificationService
    {
        public List<Notification> Created { get; } = [];

        public Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false) => Task.FromResult(Created);

        public Task<Notification> CreateNotificationAsync(Notification notification)
        {
            Created.Add(notification);
            return Task.FromResult(notification);
        }

        public Task<bool> MarkAsReadAsync(int notificationId, int requestingUserId) => Task.FromResult(false);

        public Task<int> GetUnreadCountAsync(int userId) => Task.FromResult(0);
    }

    private sealed class FailDocumentSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<Document>().Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new DbUpdateException("Injected document save failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}