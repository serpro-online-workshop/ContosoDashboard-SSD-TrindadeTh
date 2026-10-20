using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services;

public sealed class ClamAvFileScanner : IFileScanner
{
    private readonly ClamAvOptions _options;

    public ClamAvFileScanner(IOptions<ClamAvOptions> options)
    {
        _options = options.Value;
    }

    public async Task<FileScanResult> ScanAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(_options.SocketPath), timeout.Token);
            await SendAllAsync(socket, "zINSTREAM\0"u8.ToArray(), timeout.Token);

            var buffer = new byte[64 * 1024];
            var lengthBuffer = new byte[sizeof(int)];
            while (true)
            {
                var bytesRead = await fileStream.ReadAsync(buffer, timeout.Token);
                if (bytesRead == 0)
                {
                    break;
                }

                BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, bytesRead);
                await SendAllAsync(socket, lengthBuffer, timeout.Token);
                await SendAllAsync(socket, buffer.AsMemory(0, bytesRead), timeout.Token);
            }

            BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, 0);
            await SendAllAsync(socket, lengthBuffer, timeout.Token);

            var response = await ReadResponseAsync(socket, timeout.Token);
            if (response.EndsWith(" OK", StringComparison.Ordinal))
            {
                return new FileScanResult(FileScanVerdict.Clean, response);
            }

            if (response.Contains(" FOUND", StringComparison.Ordinal))
            {
                return new FileScanResult(FileScanVerdict.Infected, response);
            }

            return new FileScanResult(FileScanVerdict.Unavailable, response);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new FileScanResult(FileScanVerdict.Unavailable, "The malware scanner timed out.");
        }
        catch (Exception exception) when (exception is SocketException or IOException or ArgumentException or ObjectDisposedException)
        {
            return new FileScanResult(FileScanVerdict.Unavailable, "The malware scanner is unavailable.");
        }
    }

    private static async Task SendAllAsync(Socket socket, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        while (!buffer.IsEmpty)
        {
            var sent = await socket.SendAsync(buffer, SocketFlags.None, cancellationToken);
            if (sent == 0)
            {
                throw new IOException("The malware scanner closed the connection.");
            }

            buffer = buffer[sent..];
        }
    }

    private static async Task<string> ReadResponseAsync(Socket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        using var response = new MemoryStream();
        while (response.Length < 4096)
        {
            var bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
            if (bytesRead == 0)
            {
                throw new IOException("The malware scanner returned no verdict.");
            }

            var terminator = Array.IndexOf(buffer, (byte)0, 0, bytesRead);
            var count = terminator >= 0 ? terminator : bytesRead;
            response.Write(buffer, 0, count);
            if (terminator >= 0)
            {
                return Encoding.ASCII.GetString(response.ToArray()).TrimEnd('\r', '\n');
            }
        }

        throw new IOException("The malware scanner returned an invalid verdict.");
    }
}