namespace Swan.Domain.Content;

/// <summary>
/// An accepted article. Created when a writer accepts a proposed article (PLAN-v2 §6.1). Its WordPress drafts are
/// remote copies; this record is the source.
/// </summary>
public sealed class Article
{
    public Guid Id { get; set; }
    public Guid SourceDocumentId { get; set; }
    public Guid SourceCandidateId { get; set; }
    public string Title { get; set; } = "";
    public string ContentHtml { get; set; } = "";
    public string? SeoTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? Slug { get; set; }            // WordPress slug (last path segment)
    public string? CanonicalPath { get; set; }   // full URL path as written in the document
    public bool IsUnverified { get; set; }
    public Guid? FeaturedMediaId { get; set; }
    public string? FeaturedAltText { get; set; }
    public string Status { get; set; } = "Draft"; // editorial state; only Draft is used so far (PLAN-v2 §7.1)
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>Link between an article and the draft it has on one client site. One row per (article, site).</summary>
public sealed class ArticlePublication
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public string SiteId { get; set; } = "";
    public long? RemotePostId { get; set; }
    public string? RemoteStatus { get; set; }
    public string? RemoteLink { get; set; }
    public string? EditUrl { get; set; }
    public DateTime LastSyncedUtc { get; set; }
}
