using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using M5FileHost.Core;
using M5FileHost.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace M5FileHost.Web.Controllers;

[ApiController, Route("api/uploads")]
public sealed partial class UploadsController(AppDbContext database, IFileStorage storage, IFileTypeDetector detector, IProcessingQueue queue, IOptions<UploadOptions> options, ILogger<UploadsController> logger) : ControllerBase
{
    private readonly UploadOptions _options = options.Value;

    [HttpPost, Authorize, ValidateAntiForgeryToken, EnableRateLimiting("uploads")]
    [RequestFormLimits(MultipartBodyLengthLimit = 10L * 1024 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadRequest request, CancellationToken cancellationToken)
    {
        if (request.Files.Count is < 1 || request.Files.Count > 100) return BadRequest(new { message = "Select at least one file." });
        if (request.Files.Count > _options.MaxFilesPerUpload) return BadRequest(new { message = $"At most {_options.MaxFilesPerUpload} files may be uploaded at once." });
        if (!Enum.TryParse<FileVisibility>(request.Visibility, true, out var visibility)) return BadRequest(new { message = "Invalid visibility." });
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var results = new List<UploadResult>();
        var tags = ParseTags(request.Tags);

        foreach (var formFile in request.Files)
        {
            if (formFile.Length is <= 0 || formFile.Length > _options.MaxFileSizeBytes) return BadRequest(new { message = $"Each file must be between 1 byte and {_options.MaxFileSizeMb} MB." });
            var originalName = Path.GetFileName(formFile.FileName).Normalize();
            if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 255) return BadRequest(new { message = "A filename is invalid or too long." });
            var suppliedExtension = Path.GetExtension(originalName).ToLowerInvariant();
            if (_options.BlockedExtensions.Contains(suppliedExtension, StringComparer.OrdinalIgnoreCase)) return BadRequest(new { message = $"Files ending in {suppliedExtension} are blocked." });

            await using var input = formFile.OpenReadStream();
            if (!input.CanSeek) return StatusCode(500, new { message = "The server could not safely inspect this upload." });
            var detected = await detector.DetectAsync(input, originalName, cancellationToken);
            if (detected.MimeType == "application/x-executable") return BadRequest(new { message = "Executable file signatures are blocked." });
            input.Position = 0;
            var id = Guid.NewGuid();
            StoredFile stored;
            try { stored = await storage.SaveOriginalAsync(input, id, detected.Extension, _options.MaxFileSizeBytes, cancellationToken); }
            catch (InvalidDataException exception) { return BadRequest(new { message = exception.Message }); }

            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await database.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {0} FOR UPDATE", [userId], cancellationToken);
                var user = await database.Users.SingleAsync(x => x.Id == userId, cancellationToken);
                var used = await database.Files.Where(x => x.UploaderId == userId).SumAsync(x => (long?)x.OriginalSize, cancellationToken) ?? 0;
                if (used + stored.Size > user.StorageQuotaBytes)
                {
                    System.IO.File.Delete(storage.GetAbsolutePath(stored.RelativePath));
                    await transaction.RollbackAsync(cancellationToken);
                    return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "This upload would exceed your storage quota." });
                }
                var duplicate = await database.Files.AsNoTracking().FirstOrDefaultAsync(x => x.UploaderId == userId && x.Sha256 == stored.Sha256 && !x.IsHidden, cancellationToken);
                if (duplicate is not null)
                {
                    System.IO.File.Delete(storage.GetAbsolutePath(stored.RelativePath));
                    await transaction.RollbackAsync(cancellationToken);
                    results.Add(new(duplicate.Id, duplicate.Slug, duplicate.OriginalName, duplicate.ProcessingStatus));
                    continue;
                }
                var upload = new FileUpload
                {
                    Id = id,
                    Slug = CreateSlug(),
                    Title = CleanOptional(request.Title, 160),
                    Description = CleanOptional(request.Description, 2_000),
                    OriginalName = originalName,
                    StoredName = Path.GetFileName(stored.RelativePath),
                    MimeType = detected.MimeType,
                    Extension = detected.Extension,
                    Type = detected.Type,
                    OriginalSize = stored.Size,
                    Sha256 = stored.Sha256,
                    OriginalPath = stored.RelativePath,
                    Visibility = visibility,
                    IsNsfw = request.IsNsfw,
                    UploaderId = userId
                };
                foreach (var tagName in tags)
                {
                    var tag = await database.Tags.SingleOrDefaultAsync(x => x.Name == tagName, cancellationToken) ?? new Tag { Id = Guid.NewGuid(), Name = tagName };
                    upload.FileTags.Add(new FileTag { File = upload, Tag = tag });
                }
                database.Files.Add(upload);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                try { await queue.EnqueueAsync(new(upload.Id), cancellationToken); }
                catch (Exception exception) { logger.LogError(exception, "Upload {FileId} is pending but Redis enqueue failed; worker recovery will retry", upload.Id); }
                results.Add(new(upload.Id, upload.Slug, upload.OriginalName, upload.ProcessingStatus));
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                System.IO.File.Delete(storage.GetAbsolutePath(stored.RelativePath));
                throw;
            }
        }
        return Ok(new { successfulCount = results.Count, firstSlug = results.FirstOrDefault()?.Slug, errors = Array.Empty<string>() });
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? query, [FromQuery] string? uploader, [FromQuery] UploadedFileType? type, [FromQuery] string sort = "newest", [FromQuery] int page = 1, [FromQuery] int pageSize = 24, [FromQuery] bool nsfw = false, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 60);
        var term = string.IsNullOrWhiteSpace(search) ? query : search;
        var files = database.Files.AsNoTracking().Where(x => x.Visibility == FileVisibility.Public && !x.IsHidden && !x.Uploader.IsBanned && (nsfw || !x.IsNsfw));
        if (!string.IsNullOrWhiteSpace(uploader)) files = files.Where(x => x.Uploader.UserName == uploader);
        if (!string.IsNullOrWhiteSpace(term)) { var t = term.Trim(); files = files.Where(x => (x.Title != null && EF.Functions.ILike(x.Title, $"%{t}%")) || EF.Functions.ILike(x.OriginalName, $"%{t}%") || EF.Functions.ILike(x.Uploader.UserName!, $"%{t}%") || x.FileTags.Any(ft => EF.Functions.ILike(ft.Tag.Name, $"%{t}%"))); }
        if (type is not null) files = files.Where(x => x.Type == type);
        files = sort.ToLowerInvariant() switch
        {
            "oldest" => files.OrderBy(x => x.CreatedAt),
            "views" => files.OrderByDescending(x => x.ViewCount),
            "downloads" => files.OrderByDescending(x => x.DownloadCount),
            "largest" => files.OrderByDescending(x => x.OriginalSize),
            "smallest" => files.OrderBy(x => x.OriginalSize),
            _ => files.OrderByDescending(x => x.CreatedAt)
        };
        var total = await files.CountAsync(cancellationToken);
        var rows = await files.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Slug, x.OriginalName, x.MimeType, x.OriginalSize, x.IsNsfw, x.ViewCount, x.DownloadCount, x.CreatedAt, x.Visibility, x.UploaderId, Uploader = x.Uploader.UserName, Tags = x.FileTags.Select(t => t.Tag.Name).ToList() })
            .ToListAsync(cancellationToken);
        var items = rows.Select(r => new FileDto(r.Id, r.Slug, r.OriginalName, r.OriginalSize, r.MimeType, r.CreatedAt, r.Visibility.ToString(), r.Tags, r.IsNsfw, r.UploaderId, r.Uploader, r.ViewCount, r.DownloadCount)).ToList();
        return Ok(new PagedResult<FileDto>(items, total, page, pageSize, ApiMap.TotalPages(total, pageSize)));
    }

    [HttpGet("{slug}"), AllowAnonymous]
    public async Task<IActionResult> Get(string slug, CancellationToken cancellationToken)
    {
        var file = await VisibleFile(slug, cancellationToken);
        if (file is null) return NotFound();
        return Ok(ToFileDto(file));
    }

    [HttpPatch("{id:guid}"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUploadRequest request, CancellationToken cancellationToken)
    {
        var file = await database.Files.Include(x => x.Uploader).Include(x => x.FileTags).ThenInclude(x => x.Tag).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (file is null) return NotFound();
        if (!CanManage(file)) return Forbid();
        if (request.Title is not null) file.Title = CleanOptional(request.Title, 160);
        if (request.Description is not null) file.Description = CleanOptional(request.Description, 2_000);
        if (request.Visibility is not null && Enum.TryParse<FileVisibility>(request.Visibility, true, out var visibility)) file.Visibility = visibility;
        if (request.IsNsfw is not null) file.IsNsfw = request.IsNsfw.Value;
        file.UpdatedAt = DateTimeOffset.UtcNow;
        AddAdminAuditIfNeeded(file, "file.update");
        await database.SaveChangesAsync(cancellationToken);
        return Ok(ToFileDto(file));
    }

    [HttpDelete("{id:guid}"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var file = await database.Files.FindAsync([id], cancellationToken); if (file is null) return NotFound();
        if (!CanManage(file)) return Forbid();
        await storage.DeleteAllAsync(file, cancellationToken); AddAdminAuditIfNeeded(file, "file.delete"); database.Files.Remove(file); await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPost("{id:guid}/report"), ValidateAntiForgeryToken, EnableRateLimiting("reports")]
    public async Task<IActionResult> Report(Guid id, [FromBody] ReportRequest request, CancellationToken cancellationToken)
    {
        if (!await database.Files.AnyAsync(x => x.Id == id && x.Visibility == FileVisibility.Public && !x.IsHidden, cancellationToken)) return NotFound();
        Guid? reporter = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;
        database.Reports.Add(new Report { Id = Guid.NewGuid(), FileId = id, ReporterId = reporter, Reason = request.Reason.Trim(), Message = CleanOptional(request.Message, 1_000) });
        await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpGet("{slug}/content"), AllowAnonymous, EnableRateLimiting("downloads")]
    public async Task<IActionResult> Content(string slug, [FromQuery] bool download = false, CancellationToken cancellationToken = default)
    {
        var file = await VisibleFile(slug, cancellationToken); if (file is null) return NotFound();
        if (!file.IsSecurityScanned) return StatusCode(StatusCodes.Status423Locked, new { message = "This file is not available until security scanning finishes." });
        var relative = file.ProcessedPath is not null && file.ProcessedSize < file.OriginalSize ? file.ProcessedPath : file.OriginalPath;
        var path = storage.GetAbsolutePath(relative); if (!System.IO.File.Exists(path)) return NotFound();
        if (download) await database.Files.Where(x => x.Id == file.Id).ExecuteUpdateAsync(x => x.SetProperty(f => f.DownloadCount, f => f.DownloadCount + 1), cancellationToken);
        var safeInline = file.Type is UploadedFileType.Image or UploadedFileType.Gif or UploadedFileType.Video or UploadedFileType.Audio or UploadedFileType.Pdf or UploadedFileType.Text;
        return new FileStreamResult(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan), file.MimeType.Split(';')[0]) { FileDownloadName = download || !safeInline ? file.OriginalName : null, EnableRangeProcessing = true };
    }

    [HttpGet("{slug}/thumbnail"), AllowAnonymous]
    public async Task<IActionResult> Thumbnail(string slug, CancellationToken cancellationToken)
    {
        var file = await VisibleFile(slug, cancellationToken); if (file?.ThumbnailPath is null) return NotFound();
        var path = storage.GetAbsolutePath(file.ThumbnailPath); return System.IO.File.Exists(path) ? PhysicalFile(path, "image/webp", enableRangeProcessing: true) : NotFound();
    }

    [HttpPost("{id:guid}/view"), ValidateAntiForgeryToken]
    public async Task<IActionResult> View(Guid id, CancellationToken cancellationToken) { await database.Files.Where(x => x.Id == id && x.Visibility == FileVisibility.Public && !x.IsHidden).ExecuteUpdateAsync(x => x.SetProperty(f => f.ViewCount, f => f.ViewCount + 1), cancellationToken); return NoContent(); }

    private static FileDto ToFileDto(FileUpload file) => new(
        file.Id, file.Slug, file.OriginalName, file.OriginalSize, file.MimeType, file.CreatedAt,
        file.Visibility.ToString(), file.FileTags.Select(x => x.Tag.Name).ToList(), file.IsNsfw,
        file.UploaderId, file.Uploader.UserName, file.ViewCount, file.DownloadCount);

    private async Task<FileUpload?> VisibleFile(string slug, CancellationToken cancellationToken)
    {
        var file = await database.Files.Include(x => x.Uploader).Include(x => x.FileTags).ThenInclude(x => x.Tag).SingleOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (file is null) return null;
        var viewer = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
        var privileged = User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Owner));
        return (!file.IsHidden && !file.Uploader.IsBanned && file.Visibility != FileVisibility.Private) || file.UploaderId == viewer || privileged ? file : null;
    }

    private bool CanManage(FileUpload file) => file.UploaderId.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier) || User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Owner));
    private void AddAdminAuditIfNeeded(FileUpload file, string action)
    {
        var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (file.UploaderId == actor || !(User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Owner)))) return;
        database.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorId = actor, Action = action, TargetType = nameof(FileUpload), TargetId = file.Id.ToString(), IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { file.OriginalName, file.UploaderId }) });
    }
    private static string? CleanOptional(string? value, int max) { var clean = value?.Trim(); if (string.IsNullOrEmpty(clean)) return null; return clean.Length <= max ? clean : clean[..max]; }
    private static string[] ParseTags(string? value) => (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()).Where(x => TagRegex().IsMatch(x)).Distinct().Take(10).ToArray();
    private static string CreateSlug() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    [GeneratedRegex("^[a-z0-9_-]{1,32}$")] private static partial Regex TagRegex();
}

public sealed class UploadRequest
{
    [Required] public List<IFormFile> Files { get; init; } = [];
    [MaxLength(160)] public string? Title { get; init; }
    [MaxLength(2000)] public string? Description { get; init; }
    public string Visibility { get; init; } = "Public";
    public bool IsNsfw { get; init; }
    [MaxLength(350)] public string? Tags { get; init; }
}
public sealed record UpdateUploadRequest(string? Title, string? Description, string? Visibility, bool? IsNsfw);
public sealed record ReportRequest([Required, MaxLength(64)] string Reason, [MaxLength(1000)] string? Message);
