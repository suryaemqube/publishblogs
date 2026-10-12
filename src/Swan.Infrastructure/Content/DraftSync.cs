using Microsoft.EntityFrameworkCore;
using Swan.Adapters.Abstractions;
using Swan.Domain.Content;
using Swan.Domain.Identity;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Storage;

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
/// The featured image is uploaded to the site's media library once, then reused (PLAN-v2 §8.2).
/// </summary>
public sealed class DraftSync(SwanDbContext db, LocalFileStore files, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<DraftSyncOutcome> SyncAsync(
        Article article, string siteId, IContentPublisher publisher, Guid userId, string? ip, CancellationToken ct = default)
    {
        var featuredRemoteId = article.FeaturedMediaId is { } mediaId
            ? await EnsureMediaOnSiteAsync(article, mediaId, siteId, publisher, userId, ip, ct)
            : (long?)null;

        var request = new DraftRequest(
            Title: article.Title,
            ContentHtml: article.ContentHtml,
            Slug: article.Slug,
            SeoTitle: article.SeoTitle,
            MetaDescription: article.MetaDescription,
            FeaturedMediaId: featuredRemoteId);

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

    /// <summary>Uploads the image to this site once and remembers the media id. Later drafts reuse it.</summary>
    private async Task<long> EnsureMediaOnSiteAsync(Article article, Guid mediaId, string siteId,
        IContentPublisher publisher, Guid userId, string? ip, CancellationToken ct)
    {
        var existing = await db.RemoteMediaMappings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MediaFileId == mediaId && m.SiteId == siteId, ct);
        if (existing is not null) return existing.RemoteId;

        var media = await db.MediaFiles.AsNoTracking().FirstAsync(m => m.Id == mediaId, ct);
        var bytes = await files.ReadAsync(media.StorageKey, ct);
        var alt = string.IsNullOrWhiteSpace(article.FeaturedAltText) ? Path.GetFileNameWithoutExtension(media.FileName) : article.FeaturedAltText!;

        var uploaded = await publisher.UploadMediaAsync(new MediaUpload(bytes, media.FileName, media.MimeType, alt), ct);

        var now = _clock.GetUtcNow().UtcDateTime;
        db.RemoteMediaMappings.Add(new RemoteMediaMapping
        {
            Id = Guid.NewGuid(),
            MediaFileId = mediaId,
            SiteId = siteId,
            RemoteId = uploaded.RemoteId,
            RemoteUrl = uploaded.Url,
            CreatedUtc = now,
        });
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "publishing.media.uploaded",
            EntityType = "MediaFile",
            EntityId = mediaId.ToString(),
            Details = $"site={siteId} remote={uploaded.RemoteId}",
            IpAddress = ip,
            CreatedUtc = now,
        });
        await db.SaveChangesAsync(ct);
        return uploaded.RemoteId;
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
