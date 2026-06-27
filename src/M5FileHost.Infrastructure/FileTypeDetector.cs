using System.Text;
using M5FileHost.Core;

namespace M5FileHost.Infrastructure;

public sealed class FileTypeDetector : IFileTypeDetector
{
    public async ValueTask<DetectedFile> DetectAsync(Stream stream, string originalName, CancellationToken cancellationToken)
    {
        var start = stream.CanSeek ? stream.Position : 0;
        var header = new byte[560];
        var count = 0;
        while (count < header.Length)
        {
            var read = await stream.ReadAsync(header.AsMemory(count, header.Length - count), cancellationToken);
            if (read == 0) break;
            count += read;
        }
        if (stream.CanSeek) stream.Position = start;
        var bytes = header.AsSpan(0, count);
        var extension = CompoundExtension(originalName);

        if (Starts(bytes, "MZ"u8) || Starts(bytes, [0x7F, 0x45, 0x4C, 0x46]) || Starts(bytes, [0xCF, 0xFA, 0xED, 0xFE]) || Starts(bytes, [0xFE, 0xED, 0xFA, 0xCF])) return new("application/x-executable", UploadedFileType.Other, ".bin", false);
        if (Starts(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return new("image/png", UploadedFileType.Image, ".png", true);
        if (Starts(bytes, [0xFF, 0xD8, 0xFF])) return new("image/jpeg", UploadedFileType.Image, ".jpg", true);
        if (Starts(bytes, "GIF87a"u8) || Starts(bytes, "GIF89a"u8)) return new("image/gif", UploadedFileType.Gif, ".gif", true);
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return new("image/webp", UploadedFileType.Image, ".webp", true);
        if (bytes.Length >= 12 && bytes.Slice(4, 8).SequenceEqual("ftypavif"u8)) return new("image/avif", UploadedFileType.Image, ".avif", true);
        if (Starts(bytes, "%PDF-"u8)) return new("application/pdf", UploadedFileType.Pdf, ".pdf", true);
        if (Starts(bytes, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C])) return new("application/x-7z-compressed", UploadedFileType.Archive, ".7z", false);
        if (Starts(bytes, "Rar!\x1A\x07"u8)) return new("application/vnd.rar", UploadedFileType.Archive, ".rar", false);
        if (Starts(bytes, [0x1F, 0x8B])) return new("application/gzip", UploadedFileType.Archive, extension is ".tar.gz" or ".tgz" ? extension : ".gz", false);
        if (Starts(bytes, [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00])) return new("application/x-xz", UploadedFileType.Archive, extension == ".tar.xz" ? extension : ".xz", false);
        if (Starts(bytes, "BZh"u8)) return new("application/x-bzip2", UploadedFileType.Archive, extension == ".tar.bz2" ? extension : ".bz2", false);
        if (bytes.Length > 262 && bytes.Slice(257, 5).SequenceEqual("ustar"u8)) return new("application/x-tar", UploadedFileType.Archive, ".tar", false);
        if (Starts(bytes, [0x50, 0x4B, 0x03, 0x04]) || Starts(bytes, [0x50, 0x4B, 0x05, 0x06]))
            return extension switch
            {
                ".docx" => new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", UploadedFileType.Document, extension, false),
                ".xlsx" => new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", UploadedFileType.Document, extension, false),
                ".pptx" => new("application/vnd.openxmlformats-officedocument.presentationml.presentation", UploadedFileType.Document, extension, false),
                _ => new("application/zip", UploadedFileType.Archive, ".zip", false)
            };
        if (bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8)) return new("video/mp4", UploadedFileType.Video, ".mp4", true);
        if (Starts(bytes, [0x1A, 0x45, 0xDF, 0xA3])) return new("video/webm", UploadedFileType.Video, ".webm", true);
        if (Starts(bytes, "OggS"u8)) return new("audio/ogg", UploadedFileType.Audio, ".ogg", true);
        if (Starts(bytes, "fLaC"u8)) return new("audio/flac", UploadedFileType.Audio, ".flac", true);
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WAVE"u8)) return new("audio/wav", UploadedFileType.Audio, ".wav", true);
        if (Starts(bytes, "ID3"u8) || (bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0)) return new("audio/mpeg", UploadedFileType.Audio, ".mp3", true);
        if (LooksLikeText(bytes)) return new("text/plain; charset=utf-8", UploadedFileType.Text, extension is ".md" or ".csv" or ".json" or ".xml" ? extension : ".txt", true);
        return new("application/octet-stream", UploadedFileType.Other, extension, false);
    }

    private static bool Starts(ReadOnlySpan<byte> input, ReadOnlySpan<byte> signature) => input.StartsWith(signature);

    private static bool LooksLikeText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Contains((byte)0)) return false;
        try { _ = new UTF8Encoding(false, true).GetString(bytes); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    private static string CompoundExtension(string name)
    {
        var lower = Path.GetFileName(name).ToLowerInvariant();
        foreach (var compound in new[] { ".tar.gz", ".tar.xz", ".tar.bz2" })
            if (lower.EndsWith(compound, StringComparison.Ordinal)) return compound;
        var extension = Path.GetExtension(lower);
        return extension.Length <= 16 && extension.Skip(1).All(char.IsAsciiLetterOrDigit) ? extension : "";
    }
}
