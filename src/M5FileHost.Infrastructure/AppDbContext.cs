using M5FileHost.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace M5FileHost.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<FileUpload> Files => Set<FileUpload>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<FileTag> FileTags => Set<FileTag>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("citext");
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(80);
            entity.Property(x => x.Bio).HasMaxLength(500);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => x.NormalizedEmail);
        });
        builder.Entity<FileUpload>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.Sha256);
            entity.HasIndex(x => new { x.Visibility, x.IsHidden, x.CreatedAt });
            entity.Property(x => x.Slug).HasMaxLength(22);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(2_000);
            entity.Property(x => x.OriginalName).HasMaxLength(255);
            entity.Property(x => x.StoredName).HasMaxLength(100);
            entity.Property(x => x.MimeType).HasMaxLength(128);
            entity.Property(x => x.Extension).HasMaxLength(16);
            entity.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(16);
            entity.HasOne(x => x.Uploader).WithMany(x => x.Files).HasForeignKey(x => x.UploaderId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Tag>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasColumnType("citext").HasMaxLength(32);
            entity.HasIndex(x => x.Name).IsUnique();
        });
        builder.Entity<FileTag>(entity =>
        {
            entity.HasKey(x => new { x.FileId, x.TagId });
            entity.HasOne(x => x.File).WithMany(x => x.FileTags).HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tag).WithMany(x => x.FileTags).HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Report>(entity =>
        {
            entity.Property(x => x.Reason).HasMaxLength(64);
            entity.Property(x => x.Message).HasMaxLength(1_000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.Action).HasMaxLength(80);
            entity.Property(x => x.TargetType).HasMaxLength(40);
            entity.Property(x => x.TargetId).HasMaxLength(80);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.DetailsJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.CreatedAt);
            entity.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<AppSetting>().HasKey(x => x.Key);
        builder.Entity<AppSetting>().Property(x => x.Key).HasMaxLength(100);
        builder.Entity<ApiToken>().HasIndex(x => x.TokenHash).IsUnique();
        builder.Entity<ApiToken>().Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
        builder.Entity<ApiToken>().Property(x => x.Name).HasMaxLength(80);
    }
}
