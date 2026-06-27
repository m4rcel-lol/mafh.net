using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using M5FileHost.Core;
using Microsoft.Extensions.Options;

namespace M5FileHost.Infrastructure;

public sealed class ClamAvScanner(IOptions<ClamAvOptions> options) : IMalwareScanner
{
    private readonly ClamAvOptions _options = options.Value;

    public async Task<MalwareScanResult> ScanAsync(string path, CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return new(true);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var client = new TcpClient();
        await client.ConnectAsync(_options.Host, _options.Port, timeout.Token);
        await using var network = client.GetStream();
        await network.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[128 * 1024];
        var length = new byte[4];
        while (true)
        {
            var read = await file.ReadAsync(buffer, timeout.Token);
            BinaryPrimitives.WriteInt32BigEndian(length, read);
            await network.WriteAsync(length, timeout.Token);
            if (read == 0) break;
            await network.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
        }
        var responseBuffer = new byte[1024];
        var responseLength = await network.ReadAsync(responseBuffer, timeout.Token);
        var response = Encoding.UTF8.GetString(responseBuffer, 0, responseLength).TrimEnd('\0', '\r', '\n');
        if (response.EndsWith("OK", StringComparison.Ordinal)) return new(true);
        if (response.Contains("FOUND", StringComparison.Ordinal)) return new(false, response);
        throw new IOException($"ClamAV returned an unexpected response: {response}");
    }
}
