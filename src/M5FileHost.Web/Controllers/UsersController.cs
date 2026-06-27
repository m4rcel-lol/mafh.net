using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using M5FileHost.Core;
using M5FileHost.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ImageMagick;

namespace M5FileHost.Web.Controllers;

[ApiController, Route("api/users")]
public sealed class UsersController(AppDbContext database, UserManager<ApplicationUser> users, IFileStorage storage, IFileTypeDetector detector) : ControllerBase
{
    [HttpGet("{username}"), AllowAnonymous]
    public async Task<IActionResult> Profile(string username, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedUserName == username.ToUpper(), cancellationToken);
        if (user is null || !user.PublicProfile) return NotFound();
        var uploads = await database.Files.AsNoTracking().Where(x => x.UploaderId == user.Id && x.Visibility == FileVisibility.Public && !x.IsHidden).OrderByDescending(x => x.CreatedAt).Take(48).Select(x => new { x.Slug, x.Title, x.OriginalName, type = x.Type.ToString(), x.OriginalSize, x.IsNsfw, x.CreatedAt, x.ViewCount, x.DownloadCount, hasThumbnail = x.ThumbnailPath != null }).ToListAsync(cancellationToken);
        return Ok(new { user.UserName, user.DisplayName, user.Bio, user.AvatarPath, user.BannerPath, user.CreatedAt, user.IsBanned, role = user.Role.ToString(), uploads });
    }

    [HttpPatch("me"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        user.DisplayName = Clean(request.DisplayName, 80);
        user.Bio = Clean(request.Bio, 500);
        user.NsfwAllowed = request.NsfwAllowed;
        user.PublicProfile = request.PublicProfile;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await users.UpdateAsync(user);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors.Select(x => x.Description) });
    }

    [HttpPost("me/password"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([FromForm, Required] string currentPassword, [FromForm, Required, MinLength(12)] string newPassword)
    {
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded ? LocalRedirect("/settings?changed=1") : LocalRedirect("/settings?error=" + Uri.EscapeDataString(string.Join(" ", result.Errors.Select(x => x.Description))));
    }

    [HttpPost("me/avatar"), Authorize, ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    public async Task<IActionResult> ChangeAvatar([FromForm] IFormFile avatar, CancellationToken cancellationToken)
    {
        if (avatar.Length is <= 0 or > 5 * 1024 * 1024) return LocalRedirect("/settings?error=" + Uri.EscapeDataString("Avatar must be under 5 MB."));
        await using var stream = avatar.OpenReadStream();
        var detected = await detector.DetectAsync(stream, avatar.FileName, cancellationToken);
        if (detected.Type != UploadedFileType.Image) return LocalRedirect("/settings?error=" + Uri.EscapeDataString("Avatar must be a PNG, JPEG, WebP, or AVIF image."));
        stream.Position = 0; var info = new MagickImageInfo(stream);
        if ((long)info.Width * info.Height > 20_000_000) return LocalRedirect("/settings?error=" + Uri.EscapeDataString("Avatar dimensions are too large."));
        stream.Position = 0; using var image = new MagickImage(stream);
        image.AutoOrient(); image.Strip(); image.Resize(new MagickGeometry(512, 512)); image.Format = MagickFormat.WebP; image.Quality = 82;
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        var relative = Path.Combine("avatars", $"{user.Id:N}.webp"); var path = storage.GetAbsolutePath(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); image.Write(path);
        if (user.AvatarPath is not null && user.AvatarPath != relative) System.IO.File.Delete(storage.GetAbsolutePath(user.AvatarPath));
        user.AvatarPath = relative; user.UpdatedAt = DateTimeOffset.UtcNow; await users.UpdateAsync(user);
        return LocalRedirect("/settings?avatar=1");
    }

    [HttpGet("{username}/avatar"), AllowAnonymous]
    public async Task<IActionResult> Avatar(string username, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedUserName == username.ToUpper() && x.PublicProfile, cancellationToken);
        if (user?.AvatarPath is null) return NotFound(); var path = storage.GetAbsolutePath(user.AvatarPath);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/webp") : NotFound();
    }

    private static string? Clean(string? value, int max) { var clean = value?.Trim(); return string.IsNullOrEmpty(clean) ? null : clean[..Math.Min(clean.Length, max)]; }
}

public sealed record UpdateProfileRequest(string? DisplayName, string? Bio, bool NsfwAllowed, bool PublicProfile);
