using Microsoft.EntityFrameworkCore;
using Swan.Domain.Content;
using Swan.Domain.Identity;
using Swan.Infrastructure.Persistence;

namespace Swan.Infrastructure.Content;

/// <summary>Raised when an action is not allowed in the current state. Mapped to 409 by the API.</summary>
public sealed class ArticleStateException(string message) : Exception(message);

public sealed class ArticleService(SwanDbContext db, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>
    /// Turns a proposed article into an Article. Refused if the proposal was already accepted, so one proposal
    /// can never create two articles.
    /// </summary>
    public async Task<Article> AcceptCandidateAsync(Guid candidateId, Guid userId, string? ip, CancellationToken ct = default)
    {
        var candidate = await db.ArticleCandidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct)
            ?? throw new KeyNotFoundException("Proposed article not found.");

        if (candidate.State == "Accepted")
        {
            var existing = await db.Articles.AsNoTracking().FirstAsync(a => a.SourceCandidateId == candidateId, ct);
            return existing;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var documentSiteId = await db.ArticleDocuments.AsNoTracking()
            .Where(d => d.Id == candidate.DocumentId)
            .Select(d => d.SiteId)
            .FirstOrDefaultAsync(ct);

        var article = new Article
        {
            Id = Guid.NewGuid(),
            SourceDocumentId = candidate.DocumentId,
            SourceCandidateId = candidate.Id,
            SiteId = documentSiteId,
            Title = candidate.ProposedTitle,
            ContentHtml = candidate.ContentHtml,
            SeoTitle = candidate.SeoTitle,
            MetaDescription = candidate.MetaDescription,
            Slug = candidate.Slug,
            CanonicalPath = candidate.CanonicalPath,
            IsUnverified = candidate.IsUnverified,
            Status = "Draft",
            CreatedByUserId = userId,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        candidate.State = "Accepted";
        db.Articles.Add(article);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "article.accepted",
            EntityType = "Article",
            EntityId = article.Id.ToString(),
            Details = $"candidate={candidate.Id} title={article.Title}",
            IpAddress = ip,
            CreatedUtc = now,
        });
        await db.SaveChangesAsync(ct);
        return article;
    }

    public async Task<IReadOnlyList<Article>> ListAsync(CancellationToken ct = default) =>
        await db.Articles.AsNoTracking().OrderByDescending(a => a.CreatedUtc).ToListAsync(ct);

    public async Task<Article?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>Inline pictures of the article's source proposal, in document order.</summary>
    public async Task<IReadOnlyList<ArticleImage>> ImagesAsync(Guid candidateId, CancellationToken ct = default) =>
        await db.ArticleImages.AsNoTracking().Where(i => i.CandidateId == candidateId).OrderBy(i => i.Ordinal).ToListAsync(ct);

    public async Task<IReadOnlyList<ArticlePublication>> PublicationsAsync(Guid articleId, CancellationToken ct = default) =>
        await db.ArticlePublications.AsNoTracking().Where(p => p.ArticleId == articleId).ToListAsync(ct);
}
