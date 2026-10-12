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
