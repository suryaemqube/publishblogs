using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Swan.Domain.Content;
using Swan.Domain.Identity;
using Swan.Infrastructure.Media;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Storage;

namespace Swan.Infrastructure.Content;

/// <summary>Raised for an image that is not allowed. The message is shown to the writer.</summary>
public sealed class ImageRejectedException(string message) : Exception(message);

/// <summary>
/// Sets an article's featured image (PLAN-v2 §8.2, §6.6). Any JPG, PNG or WebP is converted to WebP; the result must
/// be 500 KB or less. Alt text is required.
/// </summary>
public sealed class FeaturedImageService(SwanDbContext db, LocalFileStore files, TimeProvider? clock = null)
{
    /// <summary>Largest upload accepted before conversion. The 500 KB limit applies to the converted picture.</summary>
    public const long MaxUploadBytes = 25L * 1024 * 1024;
    public const int MaxAltLength = 250;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<MediaFile> SetAsync(Article article, byte[] bytes, string fileName, string altText, Guid userId, string? ip, CancellationToken ct = default)
    {
        var alt = altText?.Trim() ?? "";
        if (alt.Length < 5)
            throw new ImageRejectedException("Alt text is required and must describe the image (at least 5 characters).");
        if (alt.Length > MaxAltLength)
            throw new ImageRejectedException($"Alt text must be {MaxAltLength} characters or fewer.");
        if (bytes.Length > MaxUploadBytes)
            throw new ImageRejectedException("The file is larger than 25 MB.");

        var normalized = ImageConverter.Normalize(bytes);
        var hash = Convert.ToHexString(SHA256.HashData(normalized.Bytes)).ToLowerInvariant();
        var key = $"{hash}{normalized.Extension}";

        var now = _clock.GetUtcNow().UtcDateTime;
        var media = await db.MediaFiles.FirstOrDefaultAsync(m => m.ContentHash == hash, ct);
        if (media is null)
        {
            await files.SaveAsync(key, normalized.Bytes, ct);
            media = new MediaFile
            {
                Id = Guid.NewGuid(),
                ContentHash = hash,
                StorageKey = key,
                FileName = Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(fileName) ? "featured" : Path.GetFileName(fileName)) + normalized.Extension,
                MimeType = normalized.MimeType,
                ByteSize = normalized.Bytes.Length,
                UploadedByUserId = userId,
                UploadedUtc = now,
            };
            db.MediaFiles.Add(media);
        }

        var tracked = await db.Articles.FirstAsync(a => a.Id == article.Id, ct);
        tracked.FeaturedMediaId = media.Id;
        tracked.FeaturedAltText = alt;
        tracked.UpdatedUtc = now;

        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "article.featured_image.set",
            EntityType = "Article",
            EntityId = article.Id.ToString(),
            Details = $"media={media.Id} bytes={normalized.Bytes.Length} converted={normalized.Converted}",
            IpAddress = ip,
            CreatedUtc = now,
        });

        await db.SaveChangesAsync(ct);
        return media;
    }

    public async Task<(byte[] Bytes, MediaFile Media)?> ReadAsync(Article article, CancellationToken ct = default)
    {
        if (article.FeaturedMediaId is not { } id) return null;
        var media = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (media is null) return null;
        return (await files.ReadAsync(media.StorageKey, ct), media);
    }
}
