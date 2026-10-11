namespace Swan.Domain;

/// <summary>
/// Editorial state of an article. Only the states the first build needs.
/// Full state machine: PLAN-v2 §7.1.
/// </summary>
public enum ArticleStatus
{
    Draft,
    InReview,
    Approved,
    Published,
}

/// <summary>
/// An article as Swan holds it before any client site sees it.
/// The WordPress draft is a remote copy of this; it is not the canonical record.
/// </summary>
public sealed record Article(
    Guid Id,
    string Title,
    string ContentHtml,
    string? Excerpt,
    string? Slug,
    ArticleStatus Status);
