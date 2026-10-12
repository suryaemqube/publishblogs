using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swan.Docx;
using Swan.Domain.Content;
using Swan.Domain.Identity;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Storage;

namespace Swan.Infrastructure.Content;

public sealed record IngestionOutcome(
    ArticleDocument Document,
    IReadOnlyList<ArticleCandidate> Candidates,
    ConversionResult Report,
    bool Duplicate,
    IReadOnlyList<ArticleImage>? Images = null);

/// <summary>Raised when an override is requested without permission. Mapped to 403 by the API.</summary>
public sealed class OverrideNotPermittedException(string message) : Exception(message);

/// <summary>
/// Upload pipeline (PLAN-v2 §6.2): validate, hash, deduplicate, store the original, convert against the house
/// style, and persist either proposed articles or a rejection. Every upload is audited.
/// </summary>
public sealed class DocumentIngestion(SwanDbContext db, LocalFileStore files, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Dictionary<string, MediaFile> _addedThisUpload = new(StringComparer.Ordinal);

    public async Task<IngestionOutcome> IngestAsync(
        byte[] bytes,
        string fileName,
        Guid userId,
        bool importAnyway,
        bool userMayImportUnverified,
        string? ip,
        CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || !safeName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            throw new DocxFormatException("Only .docx files can be uploaded.");

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        _addedThisUpload.Clear();

        // Dedup: the same file is never converted or stored twice.
        var existing = await db.ArticleDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.ContentHash == hash, ct);
        if (existing is not null)
        {
            var (cands, report, imgs) = await LoadAsync(existing, ct);
            return new IngestionOutcome(existing, cands, report, Duplicate: true, imgs);
        }

        if (importAnyway && !userMayImportUnverified)
            throw new OverrideNotPermittedException("Importing unverified documents needs the article.import_unverified permission.");

        using var stream = new MemoryStream(bytes, writable: false);
        var package = DocxReader.Read(stream);
        var conversion = HouseStyleConverter.Convert(package);

        var now = _clock.GetUtcNow().UtcDateTime;
        var doc = new ArticleDocument
        {
            Id = Guid.NewGuid(),
            FileName = safeName,
            FileSizeBytes = bytes.Length,
            ContentHash = hash,
            StorageKey = $"{hash}.docx",
            TotalParagraphs = package.Paragraphs.Count,
            UploadedByUserId = userId,
            UploadedUtc = now,
        };

        var candidates = new List<ArticleCandidate>();
        var images = new List<ArticleImage>();
        ConversionResult stored;

        if (conversion.Conforming)
        {
            doc.Status = DocumentStatus.Conforming;
            foreach (var c in conversion.Candidates)
            {
                var entity = ToEntity(doc.Id, c, unverified: false);
                candidates.Add(entity);
                images.AddRange(await StoreImagesAsync(entity.Id, c.ImageAssets, ct));
            }
            stored = conversion;
        }
        else if (importAnyway)
        {
            // Audited override: one unverified article, no split, no inference (PLAN-v2 §6.3.1).
            doc.Status = DocumentStatus.ImportedUnverified;
            doc.ImportedWithOverride = true;
            var whole = HouseStyleConverter.ConvertAsSingleUnverifiedArticle(package,
                Path.GetFileNameWithoutExtension(safeName));
            if (whole is not null)
            {
                var entity = ToEntity(doc.Id, whole, unverified: true);
                candidates.Add(entity);
                images.AddRange(await StoreImagesAsync(entity.Id, whole.ImageAssets, ct));
            }
            stored = conversion;
        }
        else
        {
            doc.Status = DocumentStatus.Rejected;
            stored = conversion;
        }

        // The report keeps everything except the candidate bodies, which live in ArticleCandidates.
        doc.ReportJson = JsonSerializer.Serialize(stored with { Candidates = [] });

        await files.SaveAsync(doc.StorageKey, bytes, ct);
        db.ArticleDocuments.Add(doc);
        db.ArticleCandidates.AddRange(candidates);
        db.ArticleImages.AddRange(images);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = doc.ImportedWithOverride ? "document.imported_unverified" : $"document.{doc.Status.ToString().ToLowerInvariant()}",
            EntityType = "ArticleDocument",
            EntityId = doc.Id.ToString(),
            Details = $"file={safeName} hash={hash[..12]} candidates={candidates.Count} reasons={conversion.RejectReasons.Count}",
            IpAddress = ip,
            CreatedUtc = now,
        });
        await db.SaveChangesAsync(ct);

        return new IngestionOutcome(doc, candidates, stored, Duplicate: false, images);
    }

    public async Task<(IReadOnlyList<ArticleCandidate> Candidates, ConversionResult Report, IReadOnlyList<ArticleImage> Images)> LoadAsync(
        ArticleDocument doc, CancellationToken ct = default)
    {
        var candidates = await db.ArticleCandidates.AsNoTracking()
            .Where(c => c.DocumentId == doc.Id)
            .OrderBy(c => c.CandidateIndex)
            .ToListAsync(ct);
        var ids = candidates.Select(c => c.Id).ToList();
        var images = await db.ArticleImages.AsNoTracking()
            .Where(i => ids.Contains(i.CandidateId))
            .OrderBy(i => i.Ordinal)
            .ToListAsync(ct);
        var report = JsonSerializer.Deserialize<ConversionResult>(doc.ReportJson)
                     ?? throw new InvalidOperationException("Stored report could not be read.");
        return (candidates, report, images);
    }

    /// <summary>
    /// Stores each valid picture once (by hash) and records one ArticleImage per picture. Pictures with a problem are
    /// recorded without a file, so the writer can see what to fix.
    /// </summary>
    private async Task<List<ArticleImage>> StoreImagesAsync(Guid candidateId, IReadOnlyList<DocxImageAsset> assets, CancellationToken ct)
    {
        var rows = new List<ArticleImage>();
        foreach (var a in assets)
        {
            Guid? mediaId = null;
            if (a.Problem is null && a.MimeType is not null)
            {
                var hash = Convert.ToHexString(SHA256.HashData(a.Bytes)).ToLowerInvariant();
                var media = _addedThisUpload.TryGetValue(hash, out var pending)
                    ? pending
                    : await db.MediaFiles.FirstOrDefaultAsync(m => m.ContentHash == hash, ct);
                if (media is null)
                {
                    var ext = a.MimeType == "image/webp" ? ".webp" : ".jpg";
                    var key = $"{hash}{ext}";
                    await files.SaveAsync(key, a.Bytes, ct);
                    media = new MediaFile
                    {
                        Id = Guid.NewGuid(),
                        ContentHash = hash,
                        StorageKey = key,
                        FileName = a.FileName.Length > 0 ? a.FileName : $"image{ext}",
                        MimeType = a.MimeType,
                        ByteSize = a.Bytes.Length,
                        UploadedUtc = _clock.GetUtcNow().UtcDateTime,
                    };
                    db.MediaFiles.Add(media);
                    _addedThisUpload[hash] = media;
                }
                mediaId = media.Id;
            }

            rows.Add(new ArticleImage
            {
                Id = Guid.NewGuid(),
                CandidateId = candidateId,
                Ordinal = a.Ordinal,
                Token = a.Token,
                ParagraphIndex = a.ParagraphIndex,
                MediaFileId = mediaId,
                FileName = a.FileName,
                MimeType = a.MimeType,
                AltText = a.AltText,
                Problem = a.Problem,
            });
        }
        return rows;
    }

    private static ArticleCandidate ToEntity(Guid documentId, CandidateDraft c, bool unverified) => new()
    {
        Id = Guid.NewGuid(),
        DocumentId = documentId,
        CandidateIndex = c.CandidateIndex,
        ParagraphFrom = c.ParagraphFrom,
        ParagraphTo = c.ParagraphTo,
        ProposedTitle = c.Title,
        ContentHtml = c.ContentHtml,
        WordCount = c.WordCount,
        IsUnverified = unverified,
        State = "Proposed",
        SeoTitle = c.Seo?.SeoTitle,
        MetaDescription = c.Seo?.MetaDescription,
        CanonicalPath = c.Seo?.CanonicalPath,
        Slug = c.Seo?.Slug,
        SeoWarnings = c.Seo is { Warnings.Count: > 0 } ? string.Join("\n", c.Seo.Warnings) : null,
    };
}
