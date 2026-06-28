using M5FileHost.Core;

namespace M5FileHost.Web.Controllers;

// Response shapes shared by the JSON API consumed by the React SPA. Property
// names are serialized as camelCase by the default web JSON options, matching
// the client interfaces in the former cool-frontend (src/api/types.ts).
public sealed record UserDto(
    Guid Id,
    string? Username,
    string Role,
    bool IsBanned,
    string? AvatarUrl,
    long StorageUsed,
    int UploadCount,
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
    // The SPA only distinguishes "Admin" from "User"; Owner is the seeded
    // super-admin and must unlock the admin UI, Moderator has no SPA surface.
    public static string Role(UserRole role) => role is UserRole.Admin or UserRole.Owner ? "Admin" : "User";

    public static string? AvatarUrl(ApplicationUser user) =>
        user.AvatarPath is null ? null : $"/api/users/{user.UserName}/avatar";

    public static UserDto ToUserDto(ApplicationUser user, long storageUsed, int uploadCount) =>
        new(user.Id, user.UserName, Role(user.Role), user.IsBanned, AvatarUrl(user), storageUsed, uploadCount, user.NsfwAllowed);

    public static int TotalPages(int total, int pageSize) =>
        pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
}
