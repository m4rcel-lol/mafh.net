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
using Microsoft.EntityFrameworkCore.Storage;
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
        if (request.Files.Count is < 1 || request.Files.Count > 100) return BadRequest(new { error = "Select at least one file." });
        if (request.Files.Count > _options.MaxFilesPerUpload) return BadRequest(new { error = $"At most {_options.MaxFilesPerUpload} files may be uploaded at once." });
        if (!Enum.TryParse<FileVisibility>(request.Visibility, true, out var visibility)) return BadRequest(new { error = "Invalid visibility." });
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var results = new List<UploadResult>();
        var tags = ParseTags(request.Tags);

        foreach (var formFile in request.Files)
        {
            if (formFile.Length is <= 0 || formFile.Length > _options.MaxFileSizeBytes) return BadRequest(new { error = $"Each file must be between 1 byte and {_options.MaxFileSizeMb} MB." });
            var originalName = Path.GetFileName(formFile.FileName).Normalize();
            if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 255) return BadRequest(new { error = "A filename is invalid or too long." });
            var suppliedExtension = Path.GetExtension(originalName).ToLowerInvariant();
            if (_options.BlockedExtensions.Contains(suppliedExtension, StringComparer.OrdinalIgnoreCase)) return BadRequest(new { error = $"Files ending in {suppliedExtension} are blocked." });

            await using var input = formFile.OpenReadStream();
            if (!input.CanSeek) return StatusCode(500, new { error = "The server could not safely inspect this upload." });
            var detected = await detector.DetectAsync(input, originalName, cancellationToken);
            if (detected.MimeType == "application/x-executable") return BadRequest(new { error = "Executable file signatures are blocked." });
            input.Position = 0;
            var id = Guid.NewGuid();
            var slug = CreateSlug();
            StoredFile stored;
            try { stored = await storage.SaveOriginalAsync(input, id, detected.Extension, _options.MaxFileSizeBytes, cancellationToken); }
            catch (InvalidDataException exception) { return BadRequest(new { error = exception.Message }); }

            UploadPersistenceResult persistence;
            try
            {
                var strategy = database.Database.CreateExecutionStrategy();
                persistence = await strategy.ExecuteInTransactionAsync(
                    async token =>
                    {
                        // A failed attempt can leave Added entities tracked. Each retry must
                        // rebuild its unit of work from database state.
                        database.ChangeTracker.Clear();
                        await database.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {0} FOR UPDATE", [userId], token);
                        var user = await database.Users.SingleAsync(x => x.Id == userId, token);
                        var used = await database.Files.Where(x => x.UploaderId == userId).SumAsync(x => (long?)x.OriginalSize, token) ?? 0;
                        if (used + stored.Size > user.StorageQuotaBytes) return UploadPersistenceResult.QuotaExceeded();

                        var duplicate = await database.Files.AsNoTracking().FirstOrDefaultAsync(x => x.UploaderId == userId && x.Sha256 == stored.Sha256 && !x.IsHidden, token);
                        if (duplicate is not null)
                        {
                            return UploadPersistenceResult.Duplicate(new(duplicate.Id, duplicate.Slug, duplicate.OriginalName, duplicate.ProcessingStatus));
                        }

                        var upload = new FileUpload
                        {
                            Id = id,
                            Slug = slug,
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
                            var tag = await database.Tags.SingleOrDefaultAsync(x => x.Name == tagName, token) ?? new Tag { Id = Guid.NewGuid(), Name = tagName };
                            upload.FileTags.Add(new FileTag { File = upload, Tag = tag });
                        }
                        database.Files.Add(upload);
                        await database.SaveChangesAsync(token);
                        return UploadPersistenceResult.Created(new(upload.Id, upload.Slug, upload.OriginalName, upload.ProcessingStatus));
                    },
                    token => database.Files.AsNoTracking().AnyAsync(x => x.Id == id, token),
                    IsolationLevel.Serializable,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                await DeleteStoredFileIfUncommittedAsync(id, stored, exception);
                throw;
            }

            if (persistence.Outcome == UploadPersistenceOutcome.QuotaExceeded)
            {
                DeleteStoredFile(stored);
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "This upload would exceed your storage quota." });
            }
            if (persistence.Outcome == UploadPersistenceOutcome.Duplicate) DeleteStoredFile(stored);
            else
            {
                try { await queue.EnqueueAsync(new(id), cancellationToken); }
                catch (Exception exception) { logger.LogError(exception, "Upload {FileId} is pending but Redis enqueue failed; worker recovery will retry", id); }
            }
            results.Add(persistence.Result!);
        }
        return Ok(results);
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] UploadedFileType? type, [FromQuery] string sort = "newest", [FromQuery] int page = 1, [FromQuery] int pageSize = 24, [FromQuery] bool nsfw = false, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 60);
        var query = database.Files.AsNoTracking().Where(x => x.Visibility == FileVisibility.Public && !x.IsHidden && !x.Uploader.IsBanned && (nsfw || !x.IsNsfw));
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => (x.Title != null && EF.Functions.ILike(x.Title, $"%{term}%")) || EF.Functions.ILike(x.OriginalName, $"%{term}%") || EF.Functions.ILike(x.Uploader.UserName!, $"%{term}%")); }
        if (type is not null) query = query.Where(x => x.Type == type);
        query = sort.ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(x => x.CreatedAt),
            "views" => query.OrderByDescending(x => x.ViewCount),
            "downloads" => query.OrderByDescending(x => x.DownloadCount),
            "largest" => query.OrderByDescending(x => x.OriginalSize),
            "smallest" => query.OrderBy(x => x.OriginalSize),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.Slug, x.Title, x.OriginalName, x.MimeType, type = x.Type.ToString(), x.OriginalSize, x.ProcessedSize, x.IsNsfw, x.ViewCount, x.DownloadCount, x.CreatedAt, uploader = x.Uploader.UserName, x.ThumbnailPath, status = x.ProcessingStatus.ToString() }).ToListAsync(cancellationToken);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{slug}"), AllowAnonymous]
    public async Task<IActionResult> Get(string slug, CancellationToken cancellationToken)
    {
        var file = await VisibleFile(slug, cancellationToken);
        if (file is null) return NotFound();
        return Ok(new { file.Id, file.Slug, file.Title, file.Description, file.OriginalName, file.MimeType, type = file.Type.ToString(), file.OriginalSize, file.ProcessedSize, file.Sha256, visibility = file.Visibility.ToString(), file.IsNsfw, file.ViewCount, file.DownloadCount, file.CreatedAt, uploader = file.Uploader.UserName, status = file.ProcessingStatus.ToString(), tags = file.FileTags.Select(x => x.Tag.Name) });
    }

    [HttpPatch("{id:guid}"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUploadRequest request, CancellationToken cancellationToken)
    {
        var file = await database.Files.FindAsync([id], cancellationToken); if (file is null) return NotFound();
        if (!CanManage(file)) return Forbid();
        if (request.Title is not null) file.Title = CleanOptional(request.Title, 160);
        if (request.Description is not null) file.Description = CleanOptional(request.Description, 2_000);
        if (request.Visibility is not null && Enum.TryParse<FileVisibility>(request.Visibility, true, out var visibility)) file.Visibility = visibility;
        if (request.IsNsfw is not null) file.IsNsfw = request.IsNsfw.Value;
        file.UpdatedAt = DateTimeOffset.UtcNow;
        AddAdminAuditIfNeeded(file, "file.update");
        await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpDelete("{id:guid}"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var file = await database.Files.FindAsync([id], cancellationToken); if (file is null) return NotFound();
        if (!CanManage(file)) return Forbid();
        await storage.DeleteAllAsync(file, cancellationToken); AddAdminAuditIfNeeded(file, "file.delete"); database.Files.Remove(file); await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPost("{id:guid}/report"), ValidateAntiForgeryToken, EnableRateLimiting("reports")]
    public async Task<IActionResult> Report(Guid id, [FromForm, Required, MaxLength(64)] string reason, [FromForm, MaxLength(1000)] string? message, CancellationToken cancellationToken)
    {
        if (!await database.Files.AnyAsync(x => x.Id == id && x.Visibility == FileVisibility.Public && !x.IsHidden, cancellationToken)) return NotFound();
        Guid? reporter = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;
        database.Reports.Add(new Report { Id = Guid.NewGuid(), FileId = id, ReporterId = reporter, Reason = reason.Trim(), Message = CleanOptional(message, 1_000) });
        await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpGet("{slug}/content"), AllowAnonymous, EnableRateLimiting("downloads")]
    public async Task<IActionResult> Content(string slug, [FromQuery] bool download = false, CancellationToken cancellationToken = default)
    {
        var file = await VisibleFile(slug, cancellationToken); if (file is null) return NotFound();
        if (!file.IsSecurityScanned) return StatusCode(StatusCodes.Status423Locked, new { error = "This file is not available until security scanning finishes." });
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
    private async Task DeleteStoredFileIfUncommittedAsync(Guid id, StoredFile stored, Exception uploadException)
    {
        try
        {
            database.ChangeTracker.Clear();
            if (await database.Files.AsNoTracking().AnyAsync(x => x.Id == id, CancellationToken.None)) return;
        }
        catch (Exception verificationException)
        {
            logger.LogWarning(verificationException, "Could not verify whether upload {FileId} committed; preserving its stored file", id);
            return;
        }
        logger.LogError(uploadException, "Upload {FileId} failed before its database row committed; removing its stored file", id);
        DeleteStoredFile(stored);
    }
    private void DeleteStoredFile(StoredFile stored)
    {
        try { System.IO.File.Delete(storage.GetAbsolutePath(stored.RelativePath)); }
        catch (Exception exception) { logger.LogError(exception, "Could not remove unreferenced upload at {RelativePath}", stored.RelativePath); }
    }
    [GeneratedRegex("^[a-z0-9_-]{1,32}$")] private static partial Regex TagRegex();

    private enum UploadPersistenceOutcome { Created, Duplicate, QuotaExceeded }
    private sealed record UploadPersistenceResult(UploadPersistenceOutcome Outcome, UploadResult? Result)
    {
        public static UploadPersistenceResult Created(UploadResult result) => new(UploadPersistenceOutcome.Created, result);
        public static UploadPersistenceResult Duplicate(UploadResult result) => new(UploadPersistenceOutcome.Duplicate, result);
        public static UploadPersistenceResult QuotaExceeded() => new(UploadPersistenceOutcome.QuotaExceeded, null);
    }
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
