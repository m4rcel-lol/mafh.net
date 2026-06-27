using System.Security.Cryptography;
using M5FileHost.Core;
using M5FileHost.Infrastructure;
using Microsoft.Extensions.Options;

namespace M5FileHost.Tests;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"m5-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task StreamsFileAndCalculatesHash()
    {
        var storage = CreateStorage(); var content = "safe streaming payload"u8.ToArray();
        await using var source = new MemoryStream(content);
        var result = await storage.SaveOriginalAsync(source, Guid.NewGuid(), ".txt", 1024, CancellationToken.None);
        Assert.Equal(content.Length, result.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), result.Sha256);
        Assert.True(File.Exists(storage.GetAbsolutePath(result.RelativePath)));
    }

    [Fact]
    public void RejectsTraversalAndAbsolutePaths()
    {
        var storage = CreateStorage();
        Assert.Throws<InvalidOperationException>(() => storage.GetAbsolutePath("../escape"));
        Assert.Throws<InvalidOperationException>(() => storage.GetAbsolutePath(Path.GetTempPath()));
    }

    [Fact]
    public async Task DeletesPartialFileWhenLimitIsExceeded()
    {
        var storage = CreateStorage(); await using var source = new MemoryStream(new byte[2048]);
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveOriginalAsync(source, Guid.NewGuid(), ".bin", 32, CancellationToken.None));
        Assert.Empty(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
    }

    private LocalFileStorage CreateStorage() => new(Options.Create(new UploadOptions { RootPath = _root }));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
