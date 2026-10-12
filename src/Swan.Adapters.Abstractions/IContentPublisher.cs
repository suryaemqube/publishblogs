namespace Swan.Adapters.Abstractions;

/// <summary>Content to be written to a client site as a draft.</summary>
/// <param name="Slug">WordPress slug only (no slashes). The full URL path is kept by Swan, not sent.</param>
public sealed record DraftRequest(
    string Title,
    string ContentHtml,
    string? Excerpt = null,
    string? Slug = null,
    string? SeoTitle = null,
    string? MetaDescription = null,
    long? FeaturedMediaId = null);

/// <summary>An image to put in a site's media library.</summary>
public sealed record MediaUpload(byte[] Bytes, string FileName, string MimeType, string AltText);

/// <summary>A media-library item on a site.</summary>
public sealed record RemoteMedia(long RemoteId, string? Url);

/// <summary>What the remote site reports after a draft is written.</summary>
public sealed record RemoteDraftResult(
    long RemoteId,
    string Status,
    string? Link,
    string? EditUrl);

/// <summary>Current state of a remote post, read before any update.</summary>
public sealed record RemotePostInfo(long RemoteId, string Status, string? Link, string? Slug);

/// <summary>
/// Writes content to a client site. Provider selection is by SiteIntegrations (PLAN-v2 §8.1).
/// Publishing is not on this interface yet: drafts are created and updated only.
/// </summary>
public interface IContentPublisher
{
    Task<RemoteDraftResult> CreateDraftAsync(DraftRequest request, CancellationToken ct = default);

    /// <summary>Updates an existing post only while it is a draft. Refuses published posts (see <see cref="RemotePostNotDraftException"/>).</summary>
    Task<RemoteDraftResult> UpdateDraftAsync(long remoteId, DraftRequest request, CancellationToken ct = default);

    /// <summary>Returns null when the post does not exist on the site.</summary>
    Task<RemotePostInfo?> GetPostAsync(long remoteId, CancellationToken ct = default);

    /// <summary>Uploads an image to the site's media library, with its alt text.</summary>
    Task<RemoteMedia> UploadMediaAsync(MediaUpload upload, CancellationToken ct = default);
}

/// <summary>Thrown when a site has no configured publisher. Fails loudly, never silently skips.</summary>
public sealed class PublisherNotConfiguredException(string message) : InvalidOperationException(message);

/// <summary>Thrown instead of updating a post that is already published or otherwise not a draft.</summary>
public sealed class RemotePostNotDraftException(long remoteId, string status)
    : InvalidOperationException($"Post {remoteId} is '{status}' on the site, not a draft. It was not changed.")
{
    public string Status { get; } = status;
}

/// <summary>Placeholder used for sites with no publisher configured (PLAN-v2 §15, Workstream 1).</summary>
public sealed class NoOpContentPublisher : IContentPublisher
{
    private static readonly PublisherNotConfiguredException NotConfigured =
        new("No content publisher is configured for this site.");

    public Task<RemoteDraftResult> CreateDraftAsync(DraftRequest request, CancellationToken ct = default) => throw NotConfigured;
    public Task<RemoteDraftResult> UpdateDraftAsync(long remoteId, DraftRequest request, CancellationToken ct = default) => throw NotConfigured;
    public Task<RemotePostInfo?> GetPostAsync(long remoteId, CancellationToken ct = default) => throw NotConfigured;
    public Task<RemoteMedia> UploadMediaAsync(MediaUpload upload, CancellationToken ct = default) => throw NotConfigured;
}

/// <summary>One line of a connection test result.</summary>
public sealed record ConnectionCheck(string Name, bool Passed, string Detail);

/// <summary>Result of testing a site's integration configuration (PLAN-v2 §8.4).</summary>
public sealed record ConnectionTestReport(IReadOnlyList<ConnectionCheck> Checks)
{
    public bool Passed => Checks.All(c => c.Passed);
}
