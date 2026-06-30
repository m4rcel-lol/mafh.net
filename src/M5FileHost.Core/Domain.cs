using Microsoft.AspNetCore.Identity;

namespace M5FileHost.Core;

public enum UserRole { User, Moderator, Admin, Owner }
public enum UploadedFileType { Image, Gif, Video, Audio, Archive, Document, Text, Pdf, Other }
public enum FileVisibility { Public, Unlisted, Private }
public enum ReportStatus { Pending, Resolved, Ignored }
public enum ProcessingStatus { Pending, Processing, Complete, Failed, Quarantined }

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public string? AvatarPath { get; set; }
    public string? BannerPath { get; set; }
    public string? Bio { get; set; }
    public string? Links { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsBanned { get; set; }
    public bool IsVerified { get; set; }
    public bool NsfwAllowed { get; set; }
    public bool PublicProfile { get; set; } = true;
    public long StorageQuotaBytes { get; set; } = 10L * 1024 * 1024 * 1024;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<FileUpload> Files { get; set; } = [];
}

public sealed class FileUpload
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string OriginalName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public string MimeType { get; set; } = "application/octet-stream";
    public string Extension { get; set; } = "";
    public UploadedFileType Type { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;
    public bool IsSecurityScanned { get; set; }
    public string? ProcessingError { get; set; }
    public long OriginalSize { get; set; }
    public long? ProcessedSize { get; set; }
    public string Sha256 { get; set; } = "";
    public string OriginalPath { get; set; } = "";
    public string? ProcessedPath { get; set; }
    public string? ThumbnailPath { get; set; }
    public FileVisibility Visibility { get; set; } = FileVisibility.Public;
    public bool IsNsfw { get; set; }
    public bool IsHidden { get; set; }
    public int DownloadCount { get; set; }
    public int ViewCount { get; set; }
    public Guid UploaderId { get; set; }
    public ApplicationUser Uploader { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<FileTag> FileTags { get; set; } = [];
    public ICollection<Report> Reports { get; set; } = [];
}

public sealed class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public ICollection<FileTag> FileTags { get; set; } = [];
}

public sealed class FileTag
{
    public Guid FileId { get; set; }
    public FileUpload File { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

public sealed class Report
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public FileUpload File { get; set; } = null!;
    public Guid? ReporterId { get; set; }
    public ApplicationUser? Reporter { get; set; }
    public string Reason { get; set; } = "";
    public string? Message { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? ActorId { get; set; }
    public ApplicationUser? Actor { get; set; }
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string? TargetId { get; set; }
    public string? IpAddress { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ApiToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
