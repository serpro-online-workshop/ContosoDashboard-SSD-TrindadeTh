namespace ContosoDashboard.Services;

public interface IFileStorageService
{
    Task UploadAsync(Stream fileStream, string relativePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string relativePath, CancellationToken cancellationToken = default);
    string GetAuthorizedUrl(int documentId);
}

public sealed class FileStorageOptions
{
    public string RootPath { get; set; } = "../AppData/DocumentUploads";
}