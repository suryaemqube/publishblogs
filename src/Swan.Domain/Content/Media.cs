namespace Swan.Domain.Content;

/// <summary>An image file held by Swan. Identified by content hash, so the same image is stored once.</summary>
public sealed class MediaFile
{
    public Guid Id { get; set; }
    public string ContentHash { get; set; } = "";   // SHA-256, lower-case hex
    public string StorageKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long ByteSize { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedUtc { get; set; }
}

/// <summary>
/// An inline picture from a document, tied to the proposed article it appears in. Problem is set when the picture
/// breaks a client rule; such an article cannot be sent as a draft until the picture is fixed.
/// </summary>
public sealed class ArticleImage
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public int Ordinal { get; set; }
    public string Token { get; set; } = "";          // "swan-image:N", used in the article HTML
    public int ParagraphIndex { get; set; }
    public Guid? MediaFileId { get; set; }
    public string FileName { get; set; } = "";
    public string? MimeType { get; set; }
    public string AltText { get; set; } = "";
    public string? Problem { get; set; }
}

/// <summary>
/// The WordPress media-library item for a MediaFile on one site. Created once per (file, site) and reused, so an
/// image is never uploaded twice to the same site (PLAN-v2 §8.2).
/// </summary>
public sealed class RemoteMediaMapping
{
    public Guid Id { get; set; }
    public Guid MediaFileId { get; set; }
    public string SiteId { get; set; } = "";
    public long RemoteId { get; set; }
    public string? RemoteUrl { get; set; }
    public DateTime CreatedUtc { get; set; }
}
