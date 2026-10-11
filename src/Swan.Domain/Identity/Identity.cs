namespace Swan.Domain.Identity;

/// <summary>Capability keys. Checked server-side only (PLAN-v2 §11.1).</summary>
public static class PermissionKeys
{
    public const string ArticleWrite = "article.write";
    public const string ArticleSubmit = "article.submit";
    public const string ArticleReview = "article.review";
    public const string ArticlePublish = "article.publish";
    public const string ArticleImportUnverified = "article.import_unverified";
    public const string PublishingDraft = "publishing.draft";
    public const string SiteRead = "site.read";
    public const string SiteManage = "site.manage";
    public const string UserManage = "user.manage";
    public const string AuditRead = "audit.read";

    public static readonly IReadOnlyList<string> All =
    [
        ArticleWrite, ArticleSubmit, ArticleReview, ArticlePublish, ArticleImportUnverified,
        PublishingDraft, SiteRead, SiteManage, UserManage, AuditRead,
    ];
}

/// <summary>Default roles from PLAN-v2 §11.1.</summary>
public static class RoleNames
{
    public const string Writer = "Writer";
    public const string Reviewer = "Reviewer";
    public const string Publisher = "Publisher";
    public const string ClientManager = "Client Manager";
    public const string Administrator = "Administrator";
}

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";          // stored lower-case
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntilUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsSystem { get; set; }
}

public sealed class Permission
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class RolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
}

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }
}

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";     // SHA-256 of the token; the token itself is never stored
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
}

/// <summary>Append-only record of security-relevant actions (PLAN-v2 §5.7).</summary>
public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedUtc { get; set; }
}
