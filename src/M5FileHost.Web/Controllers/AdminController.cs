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
        return Ok(new
        {
            totalUsers = await database.Users.CountAsync(cancellationToken),
            totalFiles = await database.Files.CountAsync(cancellationToken),
            totalOriginalSize = await database.Files.SumAsync(x => (long?)x.OriginalSize, cancellationToken) ?? 0,
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
    public async Task<IActionResult> Files([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var query = database.Files.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => EF.Functions.ILike(x.OriginalName, $"%{search.Trim()}%") || EF.Functions.ILike(x.Uploader.UserName!, $"%{search.Trim()}%"));
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((Math.Max(1, page) - 1) * 50).Take(50).Select(x => new { x.Id, x.Slug, x.OriginalName, x.OriginalSize, x.OriginalPath, x.IsHidden, x.IsNsfw, type = x.Type.ToString(), status = x.ProcessingStatus.ToString(), uploader = x.Uploader.UserName, x.CreatedAt }).ToListAsync(cancellationToken);
        return Ok(items);
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

    [HttpDelete("files/{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFile(Guid id, CancellationToken cancellationToken)
    {
        var file = await database.Files.FindAsync([id], cancellationToken); if (file is null) return NotFound();
        await storage.DeleteAllAsync(file, cancellationToken); database.Files.Remove(file); Audit("file.delete", "FileUpload", id.ToString(), new { file.OriginalName, file.UploaderId });
        await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = database.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => EF.Functions.ILike(x.UserName!, $"%{search.Trim()}%") || EF.Functions.ILike(x.Email!, $"%{search.Trim()}%"));
        return Ok(await query.OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new { x.Id, x.UserName, x.Email, x.DisplayName, role = x.Role.ToString(), x.IsBanned, x.CreatedAt, fileCount = x.Files.Count, storage = x.Files.Sum(f => (long?)f.OriginalSize) ?? 0 }).ToListAsync(cancellationToken));
    }

    [HttpPatch("users/{id:guid}"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] AdminUserUpdate request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.ToString()); if (user is null) return NotFound();
        if (user.Role == UserRole.Owner && !User.IsInRole(nameof(UserRole.Owner))) return Forbid();
        if (request.Role is not null)
        {
            if (!Enum.TryParse<UserRole>(request.Role, true, out var role)) return BadRequest(new { error = "Invalid role." });
            if (role == UserRole.Owner && !User.IsInRole(nameof(UserRole.Owner))) return Forbid();
            user.Role = role;
        }
        if (request.IsBanned is not null) user.IsBanned = request.IsBanned.Value;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await users.UpdateAsync(user); if (!result.Succeeded) return BadRequest(result.Errors);
        await users.UpdateSecurityStampAsync(user);
        Audit("user.update", "ApplicationUser", id.ToString(), request); await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpDelete("users/{id:guid}"), Authorize(Policy = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(Guid id, [FromQuery] bool deleteFiles, CancellationToken cancellationToken)
    {
        var user = await database.Users.Include(x => x.Files).SingleOrDefaultAsync(x => x.Id == id, cancellationToken); if (user is null) return NotFound();
        if (user.Role == UserRole.Owner) return BadRequest(new { error = "Owner accounts cannot be deleted." });
        if (user.Files.Count > 0 && !deleteFiles) return Conflict(new { error = "Set deleteFiles=true to confirm deletion of this user's uploads." });
        foreach (var file in user.Files) await storage.DeleteAllAsync(file, cancellationToken);
        if (user.AvatarPath is not null) System.IO.File.Delete(storage.GetAbsolutePath(user.AvatarPath));
        database.Files.RemoveRange(user.Files); Audit("user.delete", "ApplicationUser", id.ToString(), new { user.UserName, deletedFiles = user.Files.Count }); await database.SaveChangesAsync(cancellationToken);
        var result = await users.DeleteAsync(user); return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(CancellationToken cancellationToken) => Ok(await database.Reports.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new { x.Id, x.FileId, file = x.File.OriginalName, x.Reason, x.Message, status = x.Status.ToString(), reporter = x.Reporter != null ? x.Reporter.UserName : null, x.CreatedAt }).ToListAsync(cancellationToken));

    [HttpPatch("reports/{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReport(Guid id, [FromBody] AdminReportUpdate request, CancellationToken cancellationToken)
    {
        var report = await database.Reports.FindAsync([id], cancellationToken); if (report is null) return NotFound();
        if (!Enum.TryParse<ReportStatus>(request.Status, true, out var status) || status == ReportStatus.Pending) return BadRequest(new { error = "Status must be Resolved or Ignored." });
        report.Status = status; report.ResolvedAt = DateTimeOffset.UtcNow; Audit("report.update", "Report", id.ToString(), request); await database.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpGet("audit-log"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> AuditLog(CancellationToken cancellationToken) => Ok(await database.AuditLogs.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(250).Select(x => new { x.Id, actor = x.Actor != null ? x.Actor.UserName : null, x.Action, x.TargetType, x.TargetId, x.IpAddress, x.DetailsJson, x.CreatedAt }).ToListAsync(cancellationToken));

    private void Audit(string action, string targetType, string? targetId, object details)
    {
        database.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = action, TargetType = targetType, TargetId = targetId, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), DetailsJson = JsonSerializer.Serialize(details) });
    }
}

public sealed record AdminFileUpdate(bool? IsHidden, bool? IsNsfw);
public sealed record AdminUserUpdate(string? Role, bool? IsBanned);
public sealed record AdminReportUpdate(string Status);
