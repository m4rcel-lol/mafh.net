using System.Text;
using M5FileHost.Core;
using M5FileHost.Infrastructure;

namespace M5FileHost.Tests;

public sealed class FileTypeDetectorTests
{
    private readonly FileTypeDetector _detector = new();

    [Fact]
    public async Task DetectsPngBySignatureNotExtension()
    {
        await using var stream = new MemoryStream([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0]);
        var result = await _detector.DetectAsync(stream, "misleading.exe", CancellationToken.None);
        Assert.Equal("image/png", result.MimeType);
        Assert.Equal(UploadedFileType.Image, result.Type);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task PreservesCompoundTarGzipExtension()
    {
        await using var stream = new MemoryStream([0x1f, 0x8b, 0x08, 0x00]);
        var result = await _detector.DetectAsync(stream, "backup.tar.gz", CancellationToken.None);
        Assert.Equal(".tar.gz", result.Extension);
        Assert.Equal(UploadedFileType.Archive, result.Type);
    }

    [Fact]
    public async Task TreatsValidUtf8AsTextButBinaryNullsAsOther()
    {
        await using var text = new MemoryStream(Encoding.UTF8.GetBytes("hello, świat\n"));
        await using var binary = new MemoryStream([1, 2, 0, 4]);
        Assert.Equal(UploadedFileType.Text, (await _detector.DetectAsync(text, "readme.md", CancellationToken.None)).Type);
        Assert.Equal(UploadedFileType.Other, (await _detector.DetectAsync(binary, "blob.bin", CancellationToken.None)).Type);
    }

    [Fact]
    public async Task IdentifiesExecutableMagicBytesRegardlessOfName()
    {
        await using var stream = new MemoryStream([0x7f, 0x45, 0x4c, 0x46, 2, 1, 1]);
        var result = await _detector.DetectAsync(stream, "photo.jpg", CancellationToken.None);
        Assert.Equal("application/x-executable", result.MimeType);
        Assert.False(result.CanInline);
    }
}
