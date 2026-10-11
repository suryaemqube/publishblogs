namespace Swan.Adapters.Abstractions;

/// <summary>Content to be written to a client site as a draft.</summary>
public sealed record DraftRequest(
    string Title,
    string ContentHtml,
    string? Excerpt = null,
    string? Slug = null);

/// <summary>What the remote site reports after a draft is written.</summary>
public sealed record RemoteDraftResult(
    long RemoteId,
    string Status,
    string? Link,
    string? EditUrl);

/// <summary>
/// Writes content to a client site. Provider selection is by SiteIntegrations (PLAN-v2 §8.1).
/// Publishing is deliberately not on this interface yet: this slice only creates drafts.
/// </summary>
public interface IContentPublisher
{
    Task<RemoteDraftResult> CreateDraftAsync(DraftRequest request, CancellationToken ct = default);
}

/// <summary>Thrown when a site has no configured publisher. Fails loudly, never silently skips.</summary>
public sealed class PublisherNotConfiguredException(string message) : InvalidOperationException(message);

/// <summary>Placeholder used for sites with no publisher configured (PLAN-v2 §15, Workstream 1).</summary>
public sealed class NoOpContentPublisher : IContentPublisher
{
    public Task<RemoteDraftResult> CreateDraftAsync(DraftRequest request, CancellationToken ct = default) =>
        throw new PublisherNotConfiguredException("No content publisher is configured for this site.");
}

/// <summary>One line of a connection test result.</summary>
public sealed record ConnectionCheck(string Name, bool Passed, string Detail);

/// <summary>Result of testing a site's integration configuration (PLAN-v2 §8.4).</summary>
public sealed record ConnectionTestReport(IReadOnlyList<ConnectionCheck> Checks)
{
    public bool Passed => Checks.All(c => c.Passed);
}
