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
    bool Duplicate);

/// <summary>Raised when an override is requested without permission. Mapped to 403 by the API.</summary>
public sealed class OverrideNotPermittedException(string message) : Exception(message);

/// <summary>
/// Upload pipeline (PLAN-v2 §6.2): validate, hash, deduplicate, store the original, convert against the house
/// style, and persist either proposed articles or a rejection. Every upload is audited.
/// </summary>
public sealed class DocumentIngestion(SwanDbContext db, LocalFileStore files, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

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

        // Dedup: the same file is never converted or stored twice.
        var existing = await db.ArticleDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.ContentHash == hash, ct);
        if (existing is not null)
        {
            var (cands, report) = await LoadAsync(existing, ct);
            return new IngestionOutcome(existing, cands, report, Duplicate: true);
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
        ConversionResult stored;

        if (conversion.Conforming)
        {
            doc.Status = DocumentStatus.Conforming;
            candidates.AddRange(conversion.Candidates.Select(c => ToEntity(doc.Id, c, unverified: false)));
            stored = conversion;
        }
        else if (importAnyway)
        {
            // Audited override: one unverified article, no split, no inference (PLAN-v2 §6.3.1).
            doc.Status = DocumentStatus.ImportedUnverified;
            doc.ImportedWithOverride = true;
            var whole = HouseStyleConverter.ConvertAsSingleUnverifiedArticle(package,
                Path.GetFileNameWithoutExtension(safeName));
            if (whole is not null) candidates.Add(ToEntity(doc.Id, whole, unverified: true));
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

        return new IngestionOutcome(doc, candidates, stored, Duplicate: false);
    }

    public async Task<(IReadOnlyList<ArticleCandidate> Candidates, ConversionResult Report)> LoadAsync(ArticleDocument doc, CancellationToken ct = default)
    {
        var candidates = await db.ArticleCandidates.AsNoTracking()
            .Where(c => c.DocumentId == doc.Id)
            .OrderBy(c => c.CandidateIndex)
            .ToListAsync(ct);
        var report = JsonSerializer.Deserialize<ConversionResult>(doc.ReportJson)
                     ?? throw new InvalidOperationException("Stored report could not be read.");
        return (candidates, report);
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
    };
}
