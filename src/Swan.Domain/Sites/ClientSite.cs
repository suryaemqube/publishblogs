namespace Swan.Domain.Sites;

/// <summary>
/// A client's WordPress site, configured in the app (PLAN-v2 §5.2). Holds no secrets: the Application Password lives
/// in <see cref="SiteSecret"/>, encrypted.
/// </summary>
public sealed class ClientSite
{
    public string Id { get; set; } = "";              // stable slug, e.g. "emqube"; referenced by articles and drafts
    public string Name { get; set; } = "";
    public string RestOrigin { get; set; } = "";      // scheme://host[:port], the WordPress origin (not the public domain)
    public string PostTypeKey { get; set; } = "";
    public string RestBase { get; set; } = "";
    public string Username { get; set; } = "";
    public string TaxonomiesJson { get; set; } = "[]"; // REST-visible taxonomy names Swan writes to
    public bool WriteYoastMeta { get; set; }
    public string? UserAgent { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>"Untested" until a connection test runs, then "Ready" or "NotReady". Drafts need "Ready".</summary>
    public string ConnectionStatus { get; set; } = "Untested";
    public DateTime? LastTestedUtc { get; set; }
    public string? LastTestSummary { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>The Application Password, encrypted with ASP.NET Core Data Protection. Never returned by the API.</summary>
public sealed class SiteSecret
{
    public string SiteId { get; set; } = "";
    public string ProtectedApplicationPassword { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
}
