using System.ComponentModel.DataAnnotations;

namespace M5FileHost.Core;

public sealed class UploadOptions
{
    public const string Section = "Uploads";
    [Range(1, 10_240)] public int MaxFileSizeMb { get; init; } = 512;
    [Range(1, 100)] public int MaxFilesPerUpload { get; init; } = 20;
    [Required] public string RootPath { get; init; } = "/data/uploads";
    public bool KeepOriginalFiles { get; init; } = true;
    public string[] BlockedExtensions { get; init; } = [".exe", ".dll", ".com", ".scr", ".msi", ".bat", ".cmd", ".ps1", ".sh", ".apk", ".jar"];
    public long MaxFileSizeBytes => checked((long)MaxFileSizeMb * 1024 * 1024);
}

public sealed class ClamAvOptions
{
    public const string Section = "ClamAv";
    public bool Enabled { get; init; }
    public string Host { get; init; } = "clamav";
    [Range(1, 65535)] public int Port { get; init; } = 3310;
    [Range(1, 600)] public int TimeoutSeconds { get; init; } = 120;
}

public sealed record DetectedFile(string MimeType, UploadedFileType Type, string Extension, bool CanInline);
public sealed record ProcessingMessage(Guid FileId);
public sealed record StoredFile(string RelativePath, long Size, string Sha256);
public sealed record UploadResult(Guid Id, string Slug, string Name, ProcessingStatus Status);

public interface IFileStorage
{
    string RootPath { get; }
    Task<StoredFile> SaveOriginalAsync(Stream source, Guid fileId, string extension, long maxBytes, CancellationToken cancellationToken);
    Task DeleteAllAsync(FileUpload file, CancellationToken cancellationToken);
    string GetAbsolutePath(string relativePath);
    string GetRelativePath(Guid fileId, string category, string extension, DateTimeOffset createdAt);
}

public interface IFileTypeDetector
{
    ValueTask<DetectedFile> DetectAsync(Stream stream, string originalName, CancellationToken cancellationToken);
}

public interface IProcessingQueue
{
    Task EnqueueAsync(ProcessingMessage message, CancellationToken cancellationToken);
    Task<ProcessingMessage?> DequeueAsync(CancellationToken cancellationToken);
}

public interface IMalwareScanner
{
    Task<MalwareScanResult> ScanAsync(string path, CancellationToken cancellationToken);
}

public sealed record MalwareScanResult(bool IsClean, string? Signature = null);

public interface IFileProcessor
{
    Task ProcessAsync(Guid fileId, CancellationToken cancellationToken);
}
