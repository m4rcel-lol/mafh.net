using System.ComponentModel.DataAnnotations;
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
        var (storageUsed, uploadCount) = await Usage(user.Id, cancellationToken);
        return Ok(ApiMap.ToUserDto(user, storageUsed, uploadCount));
    }

    [HttpPatch("me"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        if (request.DisplayName is not null) user.DisplayName = Clean(request.DisplayName, 80);
        if (request.Bio is not null) user.Bio = Clean(request.Bio, 500);
        if (request.NsfwPreference is not null) user.NsfwAllowed = request.NsfwPreference.Value;
        if (request.PublicProfile is not null) user.PublicProfile = request.PublicProfile.Value;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(x => x.Description)) });
        var (storageUsed, uploadCount) = await Usage(user.Id, cancellationToken);
        return Ok(ApiMap.ToUserDto(user, storageUsed, uploadCount));
    }

    [HttpPost("me/password"), Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return result.Succeeded ? NoContent() : BadRequest(new { message = string.Join(" ", result.Errors.Select(x => x.Description)) });
    }

    [HttpPost("me/avatar"), Authorize, ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    public async Task<IActionResult> ChangeAvatar([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is <= 0 or > 5 * 1024 * 1024) return BadRequest(new { message = "Avatar must be under 5 MB." });
        await using var stream = file.OpenReadStream();
        var detected = await detector.DetectAsync(stream, file.FileName, cancellationToken);
        if (detected.Type != UploadedFileType.Image) return BadRequest(new { message = "Avatar must be a PNG, JPEG, WebP, or AVIF image." });
        stream.Position = 0; var info = new MagickImageInfo(stream);
        if ((long)info.Width * info.Height > 20_000_000) return BadRequest(new { message = "Avatar dimensions are too large." });
        stream.Position = 0; using var image = new MagickImage(stream);
        image.AutoOrient(); image.Strip(); image.Resize(new MagickGeometry(512, 512)); image.Format = MagickFormat.WebP; image.Quality = 82;
        var user = await users.GetUserAsync(User); if (user is null) return Unauthorized();
        var relative = Path.Combine("avatars", $"{user.Id:N}.webp"); var path = storage.GetAbsolutePath(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); image.Write(path);
        if (user.AvatarPath is not null && user.AvatarPath != relative) System.IO.File.Delete(storage.GetAbsolutePath(user.AvatarPath));
        user.AvatarPath = relative; user.UpdatedAt = DateTimeOffset.UtcNow; await users.UpdateAsync(user);
        var (storageUsed, uploadCount) = await Usage(user.Id, cancellationToken);
        return Ok(ApiMap.ToUserDto(user, storageUsed, uploadCount));
    }

    [HttpGet("{username}/avatar"), AllowAnonymous]
    public async Task<IActionResult> Avatar(string username, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedUserName == username.ToUpper() && x.PublicProfile, cancellationToken);
        if (user?.AvatarPath is null) return NotFound(); var path = storage.GetAbsolutePath(user.AvatarPath);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/webp") : NotFound();
    }

    private async Task<(long StorageUsed, int UploadCount)> Usage(Guid userId, CancellationToken cancellationToken)
    {
        var uploadCount = await database.Files.CountAsync(x => x.UploaderId == userId, cancellationToken);
        var storageUsed = await database.Files.Where(x => x.UploaderId == userId).SumAsync(x => (long?)x.OriginalSize, cancellationToken) ?? 0;
        return (storageUsed, uploadCount);
    }

    private static string? Clean(string? value, int max) { var clean = value?.Trim(); return string.IsNullOrEmpty(clean) ? null : clean[..Math.Min(clean.Length, max)]; }
}

public sealed record UpdateProfileRequest(string? DisplayName, string? Bio, bool? NsfwPreference, bool? PublicProfile);
public sealed record ChangePasswordRequest([Required] string CurrentPassword, [Required, MinLength(12), MaxLength(128)] string NewPassword);
