namespace ContosoDashboard.Services;

public interface IFileScanner
{
    Task<FileScanResult> ScanAsync(Stream fileStream, CancellationToken cancellationToken = default);
}

public enum FileScanVerdict
{
    Clean,
    Infected,
    Unavailable
}

public sealed record FileScanResult(FileScanVerdict Verdict, string? Details = null);

public sealed class ClamAvOptions
{
    public string SocketPath { get; set; } = "/run/clamav/clamd.ctl";
    public int TimeoutSeconds { get; set; } = 15;
}