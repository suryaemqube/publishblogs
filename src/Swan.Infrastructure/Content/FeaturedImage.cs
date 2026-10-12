using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Swan.Domain.Content;
using Swan.Domain.Identity;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Storage;

namespace Swan.Infrastructure.Content;

/// <summary>Raised for an image that is not allowed. The message is shown to the writer.</summary>
public sealed class ImageRejectedException(string message) : Exception(message);

/// <summary>Sets an article's featured image (PLAN-v2 §8.2, §6.6).</summary>
public sealed class FeaturedImageService(SwanDbContext db, LocalFileStore files, TimeProvider? clock = null)
{
    public const long MaxBytes = 500L * 1024;   // 500 KB (client decision)
    public const int MaxAltLength = 250;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<MediaFile> SetAsync(Article article, byte[] bytes, string fileName, string altText, Guid userId, string? ip, CancellationToken ct = default)
    {
        var alt = altText?.Trim() ?? "";
        if (alt.Length < 5)
            throw new ImageRejectedException("Alt text is required and must describe the image (at least 5 characters).");
        if (alt.Length > MaxAltLength)
            throw new ImageRejectedException($"Alt text must be {MaxAltLength} characters or fewer.");
        if (bytes.Length == 0)
            throw new ImageRejectedException("The image file is empty.");
        if (bytes.Length > MaxBytes)
            throw new ImageRejectedException("The image is larger than 500 KB. Compress it and upload again.");

        var (mime, ext) = Detect(bytes, fileName);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = $"{hash}{ext}";

        var now = _clock.GetUtcNow().UtcDateTime;
        var media = await db.MediaFiles.FirstOrDefaultAsync(m => m.ContentHash == hash, ct);
        if (media is null)
        {
            await files.SaveAsync(key, bytes, ct);
            media = new MediaFile
            {
                Id = Guid.NewGuid(),
                ContentHash = hash,
                StorageKey = key,
                FileName = Path.GetFileName(fileName),
                MimeType = mime,
                ByteSize = bytes.Length,
                UploadedByUserId = userId,
                UploadedUtc = now,
            };
            db.MediaFiles.Add(media);
        }

        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "article.featured_image.set",
            EntityType = "Article",
            EntityId = article.Id.ToString(),
            Details = $"media={media.Id} bytes={bytes.Length}",
            IpAddress = ip,
            CreatedUtc = now,
        });

        var tracked = await db.Articles.FirstAsync(a => a.Id == article.Id, ct);
        tracked.FeaturedMediaId = media.Id;
        tracked.FeaturedAltText = alt;
        tracked.UpdatedUtc = now;

        await db.SaveChangesAsync(ct);
        return media;
    }

    /// <summary>
    /// Accepts JPEG and WebP only (client decision). Checks the file's signature, not just its name.
    /// Returns the MIME type and storage extension.
    /// </summary>
    public static (string Mime, string Ext) Detect(byte[] b, string fileName)
    {
        var name = fileName.ToLowerInvariant();
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
        {
            if (!(name.EndsWith(".jpg") || name.EndsWith(".jpeg")))
                throw new ImageRejectedException("The file is a JPEG but its name does not end in .jpg or .jpeg.");
            return ("image/jpeg", ".jpg");
        }
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
        {
            if (!name.EndsWith(".webp")) throw new ImageRejectedException("The file is a WebP but its name does not end in .webp.");
            return ("image/webp", ".webp");
        }
        throw new ImageRejectedException("Only JPG or WebP images are accepted. Save the image as JPG or WebP and upload again.");
    }

    public async Task<(byte[] Bytes, MediaFile Media)?> ReadAsync(Article article, CancellationToken ct = default)
    {
        if (article.FeaturedMediaId is not { } id) return null;
        var media = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (media is null) return null;
        return (await files.ReadAsync(media.StorageKey, ct), media);
    }
}
