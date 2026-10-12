using Swan.Adapters.Abstractions;
using Swan.Adapters.WordPress;

namespace Swan.Api.Sites;

/// <summary>
/// Non-secret site configuration, from appsettings.json "Sites".
/// Username and ApplicationPassword are NOT here; they come from CredentialsFile.
/// </summary>
public sealed class SiteConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string RestOrigin { get; set; } = "";
    public string PostTypeKey { get; set; } = "";
    public string RestBase { get; set; } = "";
    public string CredentialsFile { get; set; } = "";

    /// <summary>Optional. Only set where the client's server blocks the default agent (PLAN: option B decision).</summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Write Yoast SEO title and meta description over REST. Stays false until the site registers the Yoast
    /// meta keys with show_in_rest (see the client setup note), otherwise WordPress ignores them silently.
    /// </summary>
    public bool WriteYoastMeta { get; set; }
}

public sealed record SiteSummary(string Id, string Name, string RestOrigin, string PostTypeKey, string RestBase);

/// <summary>Resolves a site id to a configured WordPress client. Clients are short-lived; credentials are read per use.</summary>
public sealed class SiteRegistry(IReadOnlyList<SiteConfig> sites)
{
    public IReadOnlyList<SiteSummary> List() =>
        sites.Select(s => new SiteSummary(s.Id, s.Name, s.RestOrigin, s.PostTypeKey, s.RestBase)).ToList();

    public SiteConfig? Find(string id) =>
        sites.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds a WordPress client for the site. Caller must dispose it.</summary>
    public WordPressRestClient CreateWordPressClient(SiteConfig site)
    {
        var path = ExpandPath(site.CredentialsFile);
        var settings = CredentialFile.Load(path);
        var options = new WordPressSiteOptions(
            new Uri(site.RestOrigin, UriKind.Absolute),
            site.PostTypeKey,
            site.RestBase,
            CredentialFile.Require(settings, "Username"),
            CredentialFile.Require(settings, "ApplicationPassword"),
            site.UserAgent);
        return new WordPressRestClient(options);
    }

    private static string ExpandPath(string path) =>
        Environment.ExpandEnvironmentVariables(path);
}
