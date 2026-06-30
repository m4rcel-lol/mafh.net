using System.Text.Json;
using M5FileHost.Core;
using M5FileHost.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace M5FileHost.Web.Controllers;

// Response shapes shared by the JSON API consumed by the React SPA. Property
// names are serialized as camelCase by the default web JSON options, matching
// the client interfaces in frontend/src/api/types.ts.
public sealed record ProfileLink(string Label, string Url);

public sealed record UserDto(
    Guid Id,
    string? Username,
    string? DisplayName,
    string Role,
    bool IsBanned,
    bool IsVerified,
    string? AvatarUrl,
    string? Bio,
    IReadOnlyList<ProfileLink> Links,
    long StorageUsed,
    long StorageQuota,
    int UploadCount,
    long TotalViews,
    long TotalDownloads,
    bool NsfwPreference);

public sealed record FileDto(
    Guid Id,
    string Slug,
    string FileName,
    long Size,
    string MimeType,
    DateTimeOffset UploadDate,
    string Visibility,
    IReadOnlyList<string> Tags,
    bool IsNsfw,
    Guid UploaderId,
    string? UploaderUsername,
    int Views,
    int Downloads);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);

public static class ApiMap
{
    private static readonly JsonSerializerOptions LinkJson = new(JsonSerializerDefaults.Web);

    // The SPA only distinguishes "Admin" from "User"; Owner is the seeded
    // super-admin and must unlock the admin UI, Moderator has no SPA surface.
    public static string Role(UserRole role) => role is UserRole.Admin or UserRole.Owner ? "Admin" : "User";

    // The avatar path is stable, so include a version derived from the last
    // update time to bust the browser cache when a new avatar is uploaded.
    public static string? AvatarUrl(ApplicationUser user) =>
        user.AvatarPath is null ? null : $"/api/users/{user.UserName}/avatar?v={user.UpdatedAt.ToUnixTimeMilliseconds()}";

    public static int TotalPages(int total, int pageSize) =>
        pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

    public static IReadOnlyList<ProfileLink> ParseLinks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<ProfileLink>>(json, LinkJson) ?? []; }
        catch { return []; }
    }

    // Validates and normalizes user-supplied profile links into a JSON string:
    // trims, drops empties/invalid URLs (http/https only), caps lengths and count.
    public static string NormalizeLinks(IEnumerable<ProfileLink>? links)
    {
        var clean = (links ?? [])
            .Where(l => l is not null)
            .Select(l => new ProfileLink((l.Label ?? "").Trim(), (l.Url ?? "").Trim()))
            .Where(l => l.Label.Length is > 0 and <= 40 && l.Url.Length <= 300 && IsHttpUrl(l.Url))
            .Take(8)
            .ToList();
        return JsonSerializer.Serialize(clean, LinkJson);
    }

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    // Builds the user DTO including real aggregate usage (uploads, storage,
    // views, downloads) in a single grouped query.
    public static async Task<UserDto> ToUserDtoAsync(AppDbContext database, ApplicationUser user, CancellationToken cancellationToken)
    {
        var stats = await database.Files.Where(x => x.UploaderId == user.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Size = (long?)g.Sum(f => f.OriginalSize) ?? 0,
                Views = (long?)g.Sum(f => (long)f.ViewCount) ?? 0,
                Downloads = (long?)g.Sum(f => (long)f.DownloadCount) ?? 0
            })
            .FirstOrDefaultAsync(cancellationToken);
        return new UserDto(
            user.Id, user.UserName, user.DisplayName, Role(user.Role), user.IsBanned, user.IsVerified,
            AvatarUrl(user), user.Bio, ParseLinks(user.Links),
            stats?.Size ?? 0, user.StorageQuotaBytes, stats?.Count ?? 0,
            stats?.Views ?? 0, stats?.Downloads ?? 0, user.NsfwAllowed);
    }
}
