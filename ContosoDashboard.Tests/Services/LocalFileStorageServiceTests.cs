using ContosoDashboard.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly string _contentRoot;
    private readonly string _webRoot;

    public LocalFileStorageServiceTests()
    {
        _contentRoot = Path.Combine(_root, "app");
        _webRoot = Path.Combine(_contentRoot, "wwwroot");
        Directory.CreateDirectory(_webRoot);
    }

    [Fact]
    public async Task Upload_download_and_delete_use_private_filesystem_paths()
    {
        var uploadRoot = Path.Combine(_root, "private-documents");
        var storage = CreateStorage(uploadRoot);
        var relativePath = $"4/personal/{Guid.NewGuid():N}.pdf";
        var bytes = "private bytes"u8.ToArray();
        using var input = new MemoryStream(bytes);

        await storage.UploadAsync(input, relativePath);
        await using var downloaded = await storage.DownloadAsync(relativePath);
        using var copy = new MemoryStream();
        await downloaded.CopyToAsync(copy);

        Assert.Equal(bytes, copy.ToArray());
        Assert.False(Path.GetFullPath(uploadRoot).StartsWith(Path.GetFullPath(_webRoot), StringComparison.Ordinal));
        Assert.Equal($"/documents/42/download", storage.GetAuthorizedUrl(42));

        await storage.DeleteAsync(relativePath);
        Assert.False(File.Exists(Path.Combine(uploadRoot, relativePath)));
    }

    [Fact]
    public async Task Storage_rejects_path_traversal_keys()
    {
        var storage = CreateStorage(Path.Combine(_root, "private-documents"));
        using var input = new MemoryStream("content"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(() => storage.UploadAsync(input, "../escape.pdf"));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.DownloadAsync("4/../../escape.pdf"));
    }

    [Fact]
    public void Storage_rejects_a_root_inside_the_web_root()
    {
        var options = Options.Create(new FileStorageOptions { RootPath = Path.Combine(_webRoot, "uploads") });

        Assert.Throws<InvalidOperationException>(() => new LocalFileStorageService(options, CreateEnvironment()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LocalFileStorageService CreateStorage(string root) =>
        new(Options.Create(new FileStorageOptions { RootPath = root }), CreateEnvironment());

    private IWebHostEnvironment CreateEnvironment() => new TestWebHostEnvironment
    {
        ContentRootPath = _contentRoot,
        WebRootPath = _webRoot,
        ContentRootFileProvider = new NullFileProvider(),
        WebRootFileProvider = new NullFileProvider()
    };

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ContosoDashboard.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}