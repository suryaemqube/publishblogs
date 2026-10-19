using System.Security.Claims;
using Swan.Adapters.Abstractions;
using Swan.Adapters.WordPress;
using Swan.Api.Sites;
using Swan.Domain.Content;
using Swan.Domain.Identity;
using Swan.Infrastructure.Content;
using Swan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Swan.Api.Articles;

public static class ArticleEndpoints
{
    public static void MapArticleEndpoints(this WebApplication app)
    {
        // Accept a proposed article. Idempotent: accepting twice returns the same article.
        app.MapPost("/api/candidates/{id:guid}/accept", async (Guid id, ArticleService articles, ClaimsPrincipal principal,
                                                                HttpContext http, CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue("sub")!);
            try
            {
                var article = await articles.AcceptCandidateAsync(id, userId, http.Connection.RemoteIpAddress?.ToString(), ct);
                return Results.Ok(new { article = ToDto(article), publications = Array.Empty<object>() });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).RequireAuthorization(PermissionKeys.ArticleWrite);

        var group = app.MapGroup("/api/articles").RequireAuthorization(PermissionKeys.ArticleWrite);

        // Featured image: one per article, with required alt text. Checked by file signature (PLAN-v2 §8.2).
        group.MapPost("/{id:guid}/featured-image", async (Guid id, HttpRequest request, ArticleService articles,
                                                          FeaturedImageService images, ClaimsPrincipal principal,
                                                          HttpContext http, CancellationToken ct) =>
        {
            var article = await articles.GetAsync(id, ct);
            if (article is null) return Results.NotFound(new { error = "Article not found." });
            if (!request.HasFormContentType) return Results.BadRequest(new { error = "Send the image as multipart form data." });

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null) return Results.BadRequest(new { error = "Choose an image file." });
            if (file.Length > FeaturedImageService.MaxUploadBytes) return Results.BadRequest(new { error = "The file is larger than 25 MB." });

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            try
            {
                var userId = Guid.Parse(principal.FindFirstValue("sub")!);
                var media = await images.SetAsync(article, bytes, file.FileName, form["altText"].ToString(), userId,
                                                  http.Connection.RemoteIpAddress?.ToString(), ct);
                return Results.Ok(new { mediaId = media.Id, fileName = media.FileName, mimeType = media.MimeType, altText = form["altText"].ToString().Trim() });
            }
            catch (ImageRejectedException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/{id:guid}/featured-image", async (Guid id, ArticleService articles, FeaturedImageService images, CancellationToken ct) =>
        {
            var article = await articles.GetAsync(id, ct);
            if (article is null) return Results.NotFound(new { error = "Article not found." });
            var image = await images.ReadAsync(article, ct);
            return image is null
                ? Results.NotFound(new { error = "No featured image yet." })
                : Results.File(image.Value.Bytes, image.Value.Media.MimeType);
        });

        group.MapGet("/", async (ArticleService articles, CancellationToken ct) =>
            Results.Ok((await articles.ListAsync(ct)).Select(ToDto)));

        group.MapGet("/{id:guid}", async (Guid id, ArticleService articles, CancellationToken ct) =>
        {
            var article = await articles.GetAsync(id, ct);
            if (article is null) return Results.NotFound(new { error = "Article not found." });
            var pubs = await articles.PublicationsAsync(id, ct);
            var images = await articles.ImagesAsync(article.SourceCandidateId, ct);
            return Results.Ok(new
            {
                article = ToDto(article),
                publications = pubs.Select(ToPubDto),
                images = images.Select(Swan.Api.Documents.DocumentEndpoints.ToImageDto),
                blockers = BlockersFor(article, images),
            });
        });

        // Create or update this article's draft on one site. Published posts are refused (409).
        // Body { "recreate": true } recovers when the draft was deleted on the site (409 refused="missing").
        group.MapPost("/{id:guid}/sites/{siteId}/draft", async (Guid id, string siteId, DraftSendBody? body, SiteRegistry registry,
                                                              ArticleService articles, DraftSync sync,
                                                              ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
        {
            if (!principal.HasClaim("perm", PermissionKeys.PublishingDraft))
                return Results.Json(new { error = "Your role cannot write drafts to sites." }, statusCode: StatusCodes.Status403Forbidden);

            var article = await articles.GetAsync(id, ct);
            if (article is null) return Results.NotFound(new { error = "Article not found." });

            var site = registry.Find(siteId);
            if (site is null) return Results.NotFound(new { error = $"Unknown site '{siteId}'." });

            // An article belongs to the client its document was uploaded for. Drafts go only there.
            if (article.SiteId is null)
                return Results.Json(new { error = "This article's document has no client. Re-upload the document and choose its client.", refused = "no_client" },
                                    statusCode: StatusCodes.Status409Conflict);
            if (!string.Equals(article.SiteId, siteId, StringComparison.OrdinalIgnoreCase))
                return Results.Json(new { error = $"This article belongs to client '{article.SiteId}'. Drafts can only be sent to that client.", refused = "wrong_client" },
                                    statusCode: StatusCodes.Status409Conflict);

            if (!TryCreateClient(registry, site, out var client, out var error)) return error!;
            using (client)
            {
                var publisher = new WordPressContentPublisher(client!, site.WriteYoastMeta);
                try
                {
                    var userId = Guid.Parse(principal.FindFirstValue("sub")!);
                    var outcome = await sync.SyncAsync(article, siteId, publisher, userId,
                                                       http.Connection.RemoteIpAddress?.ToString(), body?.Recreate ?? false, ct);
                    return Results.Ok(new
                    {
                        action = outcome.Action.ToString().ToLowerInvariant(),
                        remotePostId = outcome.Publication.RemotePostId,
                        remoteStatus = outcome.Publication.RemoteStatus,
                        editUrl = outcome.Publication.EditUrl,
                        remoteLink = outcome.Publication.RemoteLink,
                    });
                }
                catch (RemoteDraftPublishedException ex)
                {
                    return Results.Json(new { error = ex.Message, refused = "published", status = ex.Status },
                                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (RemotePostNotDraftException ex)
                {
                    return Results.Json(new { error = ex.Message, refused = "published", status = ex.Status },
                                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (DraftBlockedException ex)
                {
                    return Results.Json(new { error = ex.Message, refused = "not_ready" }, statusCode: StatusCodes.Status422UnprocessableEntity);
                }
                catch (RemoteDraftMissingException ex)
                {
                    return Results.Json(new { error = ex.Message, refused = "missing" }, statusCode: StatusCodes.Status409Conflict);
                }
                catch (WordPressApiException ex)
                {
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "Site rejected the request");
                }
            }
        });
    }

    /// <summary>Optional body for the draft endpoint. Recreate = the draft was deleted on the site; send a fresh one.</summary>
    private sealed record DraftSendBody(bool Recreate);

    private static bool TryCreateClient(SiteRegistry sites, SiteConfig site, out WordPressRestClient? client, out IResult? error)
    {
        try
        {
            client = sites.CreateWordPressClient(site);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or UriFormatException)
        {
            client = null;
            error = Results.Problem($"Site '{site.Id}' is not configured: {ex.Message}",
                                    statusCode: StatusCodes.Status503ServiceUnavailable, title: "Site configuration incomplete");
            return false;
        }
    }

    private static object ToDto(Article a) => new
    {
        id = a.Id,
        title = a.Title,
        status = a.Status,
        isUnverified = a.IsUnverified,
        sourceDocumentId = a.SourceDocumentId,
        sourceCandidateId = a.SourceCandidateId,
        clientSiteId = a.SiteId,
        slug = a.Slug,
        canonicalPath = a.CanonicalPath,
        seoTitle = a.SeoTitle,
        metaDescription = a.MetaDescription,
        contentHtml = a.ContentHtml,
        featuredImage = a.FeaturedMediaId is null ? null : new { mediaId = a.FeaturedMediaId, altText = a.FeaturedAltText },
        createdUtc = a.CreatedUtc,
        updatedUtc = a.UpdatedUtc,
    };

    /// <summary>Reasons a draft cannot be sent yet. Empty means ready. The same rules are enforced again when sending.</summary>
    private static string[] BlockersFor(Article article, IReadOnlyList<Swan.Domain.Content.ArticleImage> images)
    {
        var reasons = new List<string>();
        if (article.SiteId is null) reasons.Add("This article has no client. Re-upload its document and choose the client.");
        if (article.FeaturedMediaId is null) reasons.Add("Upload a featured image.");
        reasons.AddRange(images.Where(i => i.Problem is not null).Select(i => i.Problem!));
        return reasons.ToArray();
    }

    private static object ToPubDto(ArticlePublication p) => new
    {
        siteId = p.SiteId,
        remotePostId = p.RemotePostId,
        remoteStatus = p.RemoteStatus,
        editUrl = p.EditUrl,
        remoteLink = p.RemoteLink,
        lastSyncedUtc = p.LastSyncedUtc,
    };
}

