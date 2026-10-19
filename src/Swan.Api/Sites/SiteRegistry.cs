using Microsoft.AspNetCore.DataProtection;
using Swan.Adapters.WordPress;

namespace Swan.Api.Sites;

/// <summary>A configured client site, as loaded from the database. The password stays encrypted until a client is built.</summary>
public sealed class SiteConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string RestOrigin { get; set; } = "";
    public string PostTypeKey { get; set; } = "";
    public string RestBase { get; set; } = "";
    public string Username { get; set; } = "";
    public string ProtectedPassword { get; set; } = "";
    public List<string> Taxonomies { get; set; } = [];
    public bool WriteYoastMeta { get; set; }
    public string? UserAgent { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string ConnectionStatus { get; set; } = "Untested";
    public DateTime? LastTestedUtc { get; set; }
    public string? LastTestSummary { get; set; }
}

/// <summary>What the Sites screen and other callers see. Never contains the password.</summary>
public sealed record SiteSummary(
    string Id,
    string Name,
    string RestOrigin,
    string PostTypeKey,
    string RestBase,
    string Username,
    IReadOnlyList<string> Taxonomies,
    bool WriteYoastMeta,
    bool IsEnabled,
    string ConnectionStatus,
    DateTime? LastTestedUtc,
    string? LastTestSummary);

/// <summary>
/// Resolves a site id to its configuration and builds WordPress clients. Holds an in-memory snapshot of the database
/// rows, replaced by <see cref="SiteStore"/> after every change, so lookups stay synchronous and cheap.
/// </summary>
public sealed class SiteRegistry(IDataProtectionProvider protection)
{
    private readonly IDataProtector _protector = protection.CreateProtector(SiteStore.SecretPurpose);
    private volatile IReadOnlyList<SiteConfig> _sites = [];

    public void Replace(IEnumerable<SiteConfig> sites) => _sites = sites.ToList();

    public IReadOnlyList<SiteSummary> List() =>
        _sites.Select(Summarize).ToList();

    public static SiteSummary Summarize(SiteConfig s) => new(
        s.Id, s.Name, s.RestOrigin, s.PostTypeKey, s.RestBase, s.Username, s.Taxonomies, s.WriteYoastMeta,
        s.IsEnabled, s.ConnectionStatus, s.LastTestedUtc, s.LastTestSummary);

    public SiteConfig? Find(string id) =>
        _sites.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds a WordPress client for the site. The password is decrypted here and only here. Caller must dispose it.</summary>
    public WordPressRestClient CreateWordPressClient(SiteConfig site)
    {
        if (string.IsNullOrEmpty(site.ProtectedPassword))
            throw new InvalidDataException($"No Application Password is stored for site '{site.Id}'.");
        var password = _protector.Unprotect(site.ProtectedPassword);
        var options = new WordPressSiteOptions(
            new Uri(site.RestOrigin, UriKind.Absolute),
            site.PostTypeKey,
            site.RestBase,
            site.Username,
            password,
            site.UserAgent);
        return new WordPressRestClient(options);
    }
}
