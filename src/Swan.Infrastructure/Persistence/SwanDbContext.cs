using Microsoft.EntityFrameworkCore;
using Swan.Domain.Content;
using Swan.Domain.Identity;

namespace Swan.Infrastructure.Persistence;

public sealed class SwanDbContext(DbContextOptions<SwanDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ArticleDocument> ArticleDocuments => Set<ArticleDocument>();
    public DbSet<ArticleCandidate> ArticleCandidates => Set<ArticleCandidate>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(Seed.Roles);
        });

        b.Entity<Permission>(e =>
        {
            e.ToTable("Permissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Key).IsUnique();
            e.HasData(Seed.Permissions);
        });

        b.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasData(Seed.RolePermissions);
        });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("UserRoles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(88).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ArticleDocument>(e =>
        {
            e.ToTable("ArticleDocuments");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.ContentHash).IsUnique();
            e.Property(x => x.StorageKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.ReportJson).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        });

        b.Entity<ArticleCandidate>(e =>
        {
            e.ToTable("ArticleCandidates");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProposedTitle).HasMaxLength(300).IsRequired();
            e.Property(x => x.ContentHtml).IsRequired();
            e.Property(x => x.State).HasMaxLength(30).IsRequired();
            e.HasIndex(x => new { x.DocumentId, x.CandidateIndex }).IsUnique();
            e.HasOne<ArticleDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.CreatedUtc);
        });
    }
}

/// <summary>Built-in roles, permissions and their mapping. Seeded by migrations, so it is identical everywhere.</summary>
public static class Seed
{
    public static readonly Role[] Roles =
    [
        new() { Id = 1, Name = RoleNames.Writer, Description = "Uploads documents, edits articles, creates drafts.", IsSystem = true },
        new() { Id = 2, Name = RoleNames.Reviewer, Description = "Reviews and approves articles.", IsSystem = true },
        new() { Id = 3, Name = RoleNames.Publisher, Description = "Publishes approved articles.", IsSystem = true },
        new() { Id = 4, Name = RoleNames.ClientManager, Description = "Configures client sites.", IsSystem = true },
        new() { Id = 5, Name = RoleNames.Administrator, Description = "All permissions, including user management.", IsSystem = true },
    ];

    public static readonly Permission[] Permissions = PermissionKeys.All
        .Select((key, i) => new Permission { Id = i + 1, Key = key, Description = key })
        .ToArray();

    private static readonly Dictionary<int, string[]> Grants = new()
    {
        [1] = [PermissionKeys.ArticleWrite, PermissionKeys.ArticleSubmit, PermissionKeys.PublishingDraft, PermissionKeys.SiteRead],
        [2] = [PermissionKeys.ArticleReview, PermissionKeys.SiteRead],
        [3] = [PermissionKeys.ArticlePublish, PermissionKeys.PublishingDraft, PermissionKeys.SiteRead],
        [4] = [PermissionKeys.SiteRead, PermissionKeys.SiteManage],
        [5] = [.. PermissionKeys.All],
    };

    public static readonly RolePermission[] RolePermissions =
    [
        .. Grants.SelectMany(g => g.Value.Select(key => new RolePermission
        {
            RoleId = g.Key,
            PermissionId = Permissions.Single(p => p.Key == key).Id,
        })),
    ];
}
