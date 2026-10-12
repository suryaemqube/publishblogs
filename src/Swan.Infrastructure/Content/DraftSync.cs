using Microsoft.EntityFrameworkCore;
using Swan.Adapters.Abstractions;
using Swan.Domain.Content;
using Swan.Domain.Identity;
using Swan.Infrastructure.Persistence;

namespace Swan.Infrastructure.Content;

public enum DraftSyncAction { Created, Updated }

public sealed record DraftSyncOutcome(DraftSyncAction Action, ArticlePublication Publication, RemoteDraftResult Remote);

/// <summary>
/// Creates or updates an article's draft on one site (PLAN-v2 §9.2, the "update, don't duplicate" rule).
/// <list type="bullet">
/// <item>No draft on the site yet: create one and remember its post id.</item>
/// <item>Draft exists and is still a draft on the site: update that post. No second post is created.</item>
/// <item>Post is published (or otherwise not a draft): refuse. The site is not changed.</item>
/// </list>
/// </summary>
public sealed class DraftSync(SwanDbContext db, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<DraftSyncOutcome> SyncAsync(
        Article article, string siteId, IContentPublisher publisher, Guid userId, string? ip, CancellationToken ct = default)
    {
        var request = new DraftRequest(
            Title: article.Title,
            ContentHtml: article.ContentHtml,
            Slug: article.Slug,
            SeoTitle: article.SeoTitle,
            MetaDescription: article.MetaDescription);

        var pub = await db.ArticlePublications.FirstOrDefaultAsync(p => p.ArticleId == article.Id && p.SiteId == siteId, ct);
        var now = _clock.GetUtcNow().UtcDateTime;

        if (pub?.RemotePostId is { } remoteId)
        {
            // Re-check the live status first. A published post is refused, never overwritten.
            var info = await publisher.GetPostAsync(remoteId, ct);
            if (info is null)
                throw new RemoteDraftMissingException(remoteId);
            if (info.Status != "draft")
                throw new RemoteDraftPublishedException(remoteId, info.Status);

            var updated = await publisher.UpdateDraftAsync(remoteId, request, ct);
            pub.RemoteStatus = updated.Status;
            pub.RemoteLink = updated.Link;
            pub.EditUrl = updated.EditUrl;
            pub.LastSyncedUtc = now;
            Audit(userId, "publishing.draft.updated", article, siteId, remoteId, ip, now);
            await db.SaveChangesAsync(ct);
            return new DraftSyncOutcome(DraftSyncAction.Updated, pub, updated);
        }

        var created = await publisher.CreateDraftAsync(request, ct);
        pub ??= new ArticlePublication { Id = Guid.NewGuid(), ArticleId = article.Id, SiteId = siteId };
        pub.RemotePostId = created.RemoteId;
        pub.RemoteStatus = created.Status;
        pub.RemoteLink = created.Link;
        pub.EditUrl = created.EditUrl;
        pub.LastSyncedUtc = now;
        if (db.Entry(pub).State == EntityState.Detached) db.ArticlePublications.Add(pub);
        Audit(userId, "publishing.draft.created", article, siteId, created.RemoteId, ip, now);
        await db.SaveChangesAsync(ct);
        return new DraftSyncOutcome(DraftSyncAction.Created, pub, created);
    }

    private void Audit(Guid userId, string action, Article article, string siteId, long remoteId, string? ip, DateTime now) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = "Article",
            EntityId = article.Id.ToString(),
            Details = $"site={siteId} remote={remoteId}",
            IpAddress = ip,
            CreatedUtc = now,
        });
}

/// <summary>The remote post was published or changed status; the update was refused.</summary>
public sealed class RemoteDraftPublishedException(long remoteId, string status)
    : InvalidOperationException($"This article is already '{status}' on the site (post {remoteId}). Swan does not update published posts.")
{
    public string Status { get; } = status;
}

/// <summary>The remote post no longer exists.</summary>
public sealed class RemoteDraftMissingException(long remoteId)
    : InvalidOperationException($"The draft (post {remoteId}) no longer exists on the site. Nothing was changed.");
