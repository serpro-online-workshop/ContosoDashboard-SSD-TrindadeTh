using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IOptions<FileStorageOptions> options, IWebHostEnvironment environment)
    {
        var configuredPath = options.Value.RootPath;
        var absolutePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
        _rootPath = Path.GetFullPath(absolutePath);

        var webRootPath = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var webRootPrefix = Path.EndsInDirectorySeparator(webRootPath)
            ? webRootPath
            : webRootPath + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (_rootPath.Equals(webRootPath, comparison) || _rootPath.StartsWith(webRootPrefix, comparison))
        {
            throw new InvalidOperationException("The document storage root must be outside the web root.");
        }
    }

    public async Task UploadAsync(Stream fileStream, string relativePath, CancellationToken cancellationToken = default)
    {
        var destinationPath = ResolvePath(relativePath);
        var directory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(directory);

        var stagingPath = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var destination = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await fileStream.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(stagingPath, destinationPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(ResolvePath(relativePath));
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            ResolvePath(relativePath),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public string GetAuthorizedUrl(int documentId) => $"/documents/{documentId}/download";

    private string ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("A relative storage path is required.", nameof(relativePath));
        }

        var segments = relativePath.Replace('\\', '/').Split('/');
        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".." || segment.Contains(':')))
        {
            throw new ArgumentException("The storage path is not a valid relative key.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, Path.Combine(segments)));
        var rootPrefix = Path.EndsInDirectorySeparator(_rootPath)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(rootPrefix, comparison))
        {
            throw new ArgumentException("The storage path must remain beneath the configured root.", nameof(relativePath));
        }

        return fullPath;
    }
}