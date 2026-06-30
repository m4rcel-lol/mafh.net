using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using M5FileHost.Core;
using M5FileHost.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace M5FileHost.Web.Controllers;

[ApiController, Route("api/admin"), Authorize(Policy = "Moderator")]
public sealed class AdminController(AppDbContext database, IFileStorage storage, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow; var today = now.UtcDateTime.Date; var week = now.AddDays(-7);
        var totalStorage = await database.Files.SumAsync(x => (long?)x.OriginalSize, cancellationToken) ?? 0;
        return Ok(new
        {
            totalUsers = await database.Users.CountAsync(cancellationToken),
            totalFiles = await database.Files.CountAsync(cancellationToken),
            totalStorage,
            totalProcessedSize = await database.Files.SumAsync(x => x.ProcessedSize, cancellationToken) ?? 0,
            filesToday = await database.Files.CountAsync(x => x.CreatedAt >= today, cancellationToken),
            filesThisWeek = await database.Files.CountAsync(x => x.CreatedAt >= week, cancellationToken),
            pendingReports = await database.Reports.CountAsync(x => x.Status == ReportStatus.Pending, cancellationToken),
            nsfwFiles = await database.Files.CountAsync(x => x.IsNsfw, cancellationToken),
            hiddenFiles = await database.Files.CountAsync(x => x.IsHidden, cancellationToken),
            bannedUsers = await database.Users.CountAsync(x => x.IsBanned, cancellationToken)
        });
    }

    [HttpGet("files")]
    public async Task<IActionResult> Files([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = database.Files.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var t = search.Trim(); query = query.Where(x => EF.Functions.ILike(x.OriginalName, $"%{t}%") || EF.Functions.ILike(x.Uploader.UserName!, $"%{t}%")); }
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Slug, x.OriginalName, x.MimeType, x.OriginalSize, x.IsNsfw, x.ViewCount, x.DownloadCount, x.CreatedAt, x.Visibility, x.UploaderId, Uploader = x.Uploader.UserName, Tags = x.FileTags.Select(t => t.Tag.Name).ToList() })
            .ToListAsync(cancellationToken);
        var items = rows.Select(r => new FileDto(r.Id, r.Slug, r.OriginalName, r.OriginalSize, r.MimeType, r.CreatedAt, r.Visibility.ToString(), r.Tags, r.IsNsfw, r.UploaderId, r.Uploader, r.ViewCount, r.DownloadCount)).ToList();
        return Ok(new PagedResult<FileDto>(items, total, page, pageSize, ApiMap.TotalPages(total, pageSize)));
    }

    [HttpDelete("files/{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFile(Guid id, CancellationToken cancellationToken)
    {
        var file = await database.Files.FindAsync([id], cancellationToken); if (file is null) return NotFound();
        await storage.DeleteAllAsync(file, cancellationToken); database.Files.Remove(file); Audit("file.delete", "FileUpload", id.ToString(), new { file.OriginalName, file.UploaderId });
        await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPatch("files/{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFile(Guid id, [FromBody] AdminFileUpdate request, CancellationToken cancellationToken)
    {
        var file = await database.Files.FindAsync([id], cancellationToken); if (file is null) return NotFound();
        if (request.IsHidden is not null) file.IsHidden = request.IsHidden.Value;
        if (request.IsNsfw is not null) file.IsNsfw = request.IsNsfw.Value;
        file.UpdatedAt = DateTimeOffset.UtcNow;
        Audit("file.update", "FileUpload", id.ToString(), request);
        await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = database.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var t = search.Trim(); query = query.Where(x => EF.Functions.ILike(x.UserName!, $"%{t}%") || EF.Functions.ILike(x.Email!, $"%{t}%")); }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, username = x.UserName, x.Email, x.DisplayName, role = x.Role.ToString(), x.IsBanned, x.IsVerified, x.CreatedAt, fileCount = x.Files.Count, storage = x.Files.Sum(f => (long?)f.OriginalSize) ?? 0 })
            .ToListAsync(cancellationToken);
        return Ok(new { items, totalCount = total, page, pageSize, totalPages = ApiMap.TotalPages(total, pageSize) });
    }

    [HttpPatch("users/{id:guid}/role"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUserRole(Guid id, [FromBody] SetRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.ToString()); if (user is null) return NotFound();
        if (user.Role == UserRole.Owner && !User.IsInRole(nameof(UserRole.Owner))) return Forbid();
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role)) return BadRequest(new { message = "Invalid role." });
        if (role == UserRole.Owner && !User.IsInRole(nameof(UserRole.Owner))) return Forbid();
        user.Role = role; user.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await users.UpdateAsync(user); if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(x => x.Description)) });
        await users.UpdateSecurityStampAsync(user);
        Audit("user.role", "ApplicationUser", id.ToString(), new { request.Role });
        await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPatch("users/{id:guid}/suspend"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendUser(Guid id, [FromBody] SuspendRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.ToString()); if (user is null) return NotFound();
        if (user.Role == UserRole.Owner) return BadRequest(new { message = "Owner accounts cannot be suspended." });
        user.IsBanned = request.IsBanned; user.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await users.UpdateAsync(user); if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(x => x.Description)) });
        await users.UpdateSecurityStampAsync(user);
        Audit(request.IsBanned ? "user.suspend" : "user.unsuspend", "ApplicationUser", id.ToString(), new { request.IsBanned });
        await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    // Verification is a staff action: any moderator+ (the controller policy) can
    // grant or revoke the verified badge.
    [HttpPatch("users/{id:guid}/verify"), ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyUser(Guid id, [FromBody] VerifyRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.ToString()); if (user is null) return NotFound();
        user.IsVerified = request.IsVerified; user.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await users.UpdateAsync(user); if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(x => x.Description)) });
        Audit(request.IsVerified ? "user.verify" : "user.unverify", "ApplicationUser", id.ToString(), new { request.IsVerified });
        await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpDelete("users/{id:guid}"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(Guid id, [FromQuery] bool deleteFiles, CancellationToken cancellationToken)
    {
        var user = await database.Users.Include(x => x.Files).SingleOrDefaultAsync(x => x.Id == id, cancellationToken); if (user is null) return NotFound();
        if (user.Role == UserRole.Owner) return BadRequest(new { message = "Owner accounts cannot be deleted." });
        if (user.Files.Count > 0 && !deleteFiles) return Conflict(new { message = "Set deleteFiles=true to confirm deletion of this user's uploads." });
        foreach (var file in user.Files) await storage.DeleteAllAsync(file, cancellationToken);
        if (user.AvatarPath is not null) System.IO.File.Delete(storage.GetAbsolutePath(user.AvatarPath));
        database.Files.RemoveRange(user.Files); Audit("user.delete", "ApplicationUser", id.ToString(), new { user.UserName, deletedFiles = user.Files.Count }); await database.SaveChangesAsync(cancellationToken);
        var result = await users.DeleteAsync(user); return result.Succeeded ? NoContent() : BadRequest(new { message = string.Join(" ", result.Errors.Select(x => x.Description)) });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> Reports([FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = database.Reports.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.FileId, file = x.File.OriginalName, fileSlug = x.File.Slug, x.Reason, x.Message, status = x.Status.ToString(), reporter = x.Reporter != null ? x.Reporter.UserName : null, x.CreatedAt })
            .ToListAsync(cancellationToken);
        return Ok(new { items, totalCount = total, page, pageSize, totalPages = ApiMap.TotalPages(total, pageSize) });
    }

    [HttpPost("reports/{id:guid}/resolve"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveReport(Guid id, [FromBody] ResolveReportRequest request, CancellationToken cancellationToken)
    {
        var report = await database.Reports.FindAsync([id], cancellationToken); if (report is null) return NotFound();
        if (!Enum.TryParse<ReportStatus>(request.Action, true, out var status) || status == ReportStatus.Pending) return BadRequest(new { message = "Action must be Resolved or Ignored." });
        report.Status = status; report.ResolvedAt = DateTimeOffset.UtcNow; Audit("report.resolve", "Report", id.ToString(), request); await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpGet("audit"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> AuditLog([FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 250);
        var query = database.AuditLogs.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, actor = x.Actor != null ? x.Actor.UserName : null, x.Action, x.TargetType, x.TargetId, x.IpAddress, x.DetailsJson, x.CreatedAt })
            .ToListAsync(cancellationToken);
        return Ok(new { items, totalCount = total, page, pageSize, totalPages = ApiMap.TotalPages(total, pageSize) });
    }

    private void Audit(string action, string targetType, string? targetId, object details)
    {
        database.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = action, TargetType = targetType, TargetId = targetId, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), DetailsJson = JsonSerializer.Serialize(details) });
    }
}

public sealed record AdminFileUpdate(bool? IsHidden, bool? IsNsfw);
public sealed record SetRoleRequest([Required] string Role);
public sealed record SuspendRequest(bool IsBanned);
public sealed record VerifyRequest(bool IsVerified);
public sealed record ResolveReportRequest([Required] string Action);
