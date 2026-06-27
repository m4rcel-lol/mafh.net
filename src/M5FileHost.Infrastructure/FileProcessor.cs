using System.Diagnostics;
using M5FileHost.Core;
using Microsoft.EntityFrameworkCore;
using ImageMagick;

namespace M5FileHost.Infrastructure;

public sealed class FileProcessor(AppDbContext database, IFileStorage storage, IMalwareScanner scanner) : IFileProcessor
{
    public async Task ProcessAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var file = await database.Files.SingleOrDefaultAsync(x => x.Id == fileId, cancellationToken) ?? throw new InvalidOperationException("Queued file no longer exists.");
        if (file.ProcessingStatus is ProcessingStatus.Complete or ProcessingStatus.Quarantined) return;
        file.ProcessingStatus = ProcessingStatus.Processing;
        await database.SaveChangesAsync(cancellationToken);
        try
        {
            var source = storage.GetAbsolutePath(file.OriginalPath);
            var scan = await scanner.ScanAsync(source, cancellationToken);
            if (!scan.IsClean)
            {
                var quarantine = storage.GetRelativePath(file.Id, "quarantine", file.Extension, file.CreatedAt);
                var destination = storage.GetAbsolutePath(quarantine);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(source, destination, true);
                file.OriginalPath = quarantine;
                file.ProcessingStatus = ProcessingStatus.Quarantined;
                file.ProcessingError = "Malware scanner rejected this upload.";
                file.IsHidden = true;
                await database.SaveChangesAsync(cancellationToken);
                return;
            }

            file.IsSecurityScanned = true;
            await database.SaveChangesAsync(cancellationToken);

            switch (file.Type)
            {
                case UploadedFileType.Image:
                    await OptimizeImageAsync(file, source, cancellationToken);
                    break;
                case UploadedFileType.Gif:
                    await OptimizeGifAsync(file, source, cancellationToken);
                    break;
                case UploadedFileType.Video:
                    await OptimizeVideoAsync(file, source, cancellationToken);
                    break;
                case UploadedFileType.Audio:
                    await OptimizeAudioAsync(file, source, cancellationToken);
                    break;
                default:
                    file.ProcessedSize = file.OriginalSize;
                    break;
            }
            file.ProcessingStatus = ProcessingStatus.Complete;
            file.ProcessingError = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            file.ProcessingStatus = ProcessingStatus.Failed;
            file.ProcessingError = exception.Message.Length > 500 ? exception.Message[..500] : exception.Message;
        }
        file.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task OptimizeImageAsync(FileUpload file, string source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new MagickImageInfo(source);
        if ((long)info.Width * info.Height > 80_000_000) throw new InvalidDataException("Image dimensions exceed the safe processing limit.");
        using var image = new MagickImage(source);
        image.AutoOrient();
        image.Strip();
        image.Format = MagickFormat.WebP;
        image.Quality = 82;
        var processed = storage.GetRelativePath(file.Id, "processed", ".webp", file.CreatedAt);
        var processedPath = storage.GetAbsolutePath(processed);
        Directory.CreateDirectory(Path.GetDirectoryName(processedPath)!);
        image.Write(processedPath);
        cancellationToken.ThrowIfCancellationRequested();
        using var thumbnail = image.Clone();
        thumbnail.Resize(new MagickGeometry(720, 480));
        thumbnail.Quality = 76;
        var thumb = storage.GetRelativePath(file.Id, "thumbnails", ".webp", file.CreatedAt);
        var thumbPath = storage.GetAbsolutePath(thumb);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbPath)!);
        thumbnail.Write(thumbPath);
        await Task.CompletedTask;
        file.ProcessedPath = processed;
        file.ProcessedSize = new FileInfo(processedPath).Length;
        file.ThumbnailPath = thumb;
    }

    private async Task OptimizeGifAsync(FileUpload file, string source, CancellationToken cancellationToken)
    {
        var processed = storage.GetRelativePath(file.Id, "processed", ".gif", file.CreatedAt);
        var output = storage.GetAbsolutePath(processed);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await RunAsync("gifsicle", ["--optimize=3", "--colors", "256", "--output", output, source], TimeSpan.FromMinutes(5), cancellationToken);
        file.ProcessedPath = processed;
        file.ProcessedSize = new FileInfo(output).Length;
        await CreateMediaThumbnailAsync(file, source, cancellationToken);
    }

    private async Task OptimizeVideoAsync(FileUpload file, string source, CancellationToken cancellationToken)
    {
        var processed = storage.GetRelativePath(file.Id, "processed", ".mp4", file.CreatedAt);
        var output = storage.GetAbsolutePath(processed);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await RunAsync("ffmpeg", ["-nostdin", "-y", "-i", source, "-map_metadata", "-1", "-vf", "scale='min(1920,iw)':-2", "-c:v", "libx264", "-preset", "medium", "-crf", "24", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", output], TimeSpan.FromMinutes(30), cancellationToken);
        file.ProcessedPath = processed;
        file.ProcessedSize = new FileInfo(output).Length;
        await CreateMediaThumbnailAsync(file, source, cancellationToken);
    }

    private async Task OptimizeAudioAsync(FileUpload file, string source, CancellationToken cancellationToken)
    {
        var processed = storage.GetRelativePath(file.Id, "processed", ".ogg", file.CreatedAt);
        var output = storage.GetAbsolutePath(processed);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await RunAsync("ffmpeg", ["-nostdin", "-y", "-i", source, "-map_metadata", "-1", "-vn", "-c:a", "libopus", "-b:a", "128k", output], TimeSpan.FromMinutes(15), cancellationToken);
        file.ProcessedPath = processed;
        file.ProcessedSize = new FileInfo(output).Length;
    }

    private async Task CreateMediaThumbnailAsync(FileUpload file, string source, CancellationToken cancellationToken)
    {
        var thumb = storage.GetRelativePath(file.Id, "thumbnails", ".webp", file.CreatedAt);
        var output = storage.GetAbsolutePath(thumb);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await RunAsync("ffmpeg", ["-nostdin", "-y", "-ss", "00:00:01", "-i", source, "-frames:v", "1", "-vf", "scale=720:-2", output], TimeSpan.FromMinutes(2), cancellationToken);
        file.ThumbnailPath = thumb;
    }

    private static async Task RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = new() { FileName = executable, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new InvalidOperationException($"Could not start {executable}.");
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var errorTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);
        try { await process.WaitForExitAsync(timeoutSource.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw; }
        var error = await errorTask;
        if (process.ExitCode != 0) throw new InvalidOperationException($"{executable} failed: {error[^Math.Min(error.Length, 400)..]}");
    }
}
