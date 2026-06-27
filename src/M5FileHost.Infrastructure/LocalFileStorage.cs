using System.Buffers;
using System.Security.Cryptography;
using M5FileHost.Core;
using Microsoft.Extensions.Options;

namespace M5FileHost.Infrastructure;

public sealed class LocalFileStorage(IOptions<UploadOptions> options) : IFileStorage
{
    private readonly UploadOptions _options = options.Value;
    public string RootPath => Path.GetFullPath(_options.RootPath);

    public async Task<StoredFile> SaveOriginalAsync(Stream source, Guid fileId, string extension, long maxBytes, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var relative = GetRelativePath(fileId, "originals", extension, now);
        var destination = GetAbsolutePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = Path.Combine(RootPath, "temp", $"{fileId:N}.upload");
        Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);
        long total = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        try
        {
            await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0) break;
                total = checked(total + read);
                if (total > maxBytes) throw new InvalidDataException($"File exceeds the {maxBytes} byte limit.");
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            await output.FlushAsync(cancellationToken);
            var sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            output.Close();
            File.Move(temporary, destination);
            return new StoredFile(relative, total, sha256);
        }
        catch
        {
            File.Delete(temporary);
            File.Delete(destination);
            throw;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    public Task DeleteAllAsync(FileUpload file, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var relative in new[] { file.OriginalPath, file.ProcessedPath, file.ThumbnailPath }.Where(x => !string.IsNullOrWhiteSpace(x)))
            File.Delete(GetAbsolutePath(relative!));
        return Task.CompletedTask;
    }

    public string GetAbsolutePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidOperationException("Storage paths must be relative.");
        var full = Path.GetFullPath(Path.Combine(RootPath, relativePath));
        var prefix = RootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Storage path escaped the configured root.");
        return full;
    }

    public string GetRelativePath(Guid fileId, string category, string extension, DateTimeOffset createdAt)
    {
        if (category is not ("originals" or "processed" or "thumbnails" or "quarantine")) throw new ArgumentOutOfRangeException(nameof(category));
        var safeExtension = extension.Length > 0 && extension[0] == '.' && extension.Skip(1).All(char.IsAsciiLetterOrDigit) ? extension.ToLowerInvariant() : "";
        var suffix = category switch { "originals" => "-original", "processed" => "-processed", _ => "" };
        return Path.Combine(category, createdAt.ToString("yyyy"), createdAt.ToString("MM"), createdAt.ToString("dd"), $"{fileId:N}{suffix}{safeExtension}");
    }
}
