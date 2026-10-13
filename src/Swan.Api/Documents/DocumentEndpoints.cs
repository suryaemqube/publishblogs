using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swan.Docx;
using Swan.Domain.Content;
using Swan.Api.Sites;
using Swan.Domain.Identity;
using Swan.Infrastructure.Content;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Storage;

namespace Swan.Api.Documents;

public sealed record SetDocumentClientBody(string? SiteId);

public static class DocumentEndpoints
{
    private const long MaxUploadBytes = 25L * 1024 * 1024;

    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/documents").RequireAuthorization(PermissionKeys.ArticleWrite);

        // Upload. Returns 201 for a new file, 200 when the identical file was already uploaded.
        group.MapPost("/", async (HttpRequest request, DocumentIngestion ingestion, SiteRegistry registry, ClaimsPrincipal principal,
                                  HttpContext http, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(new { error = "Send the file as multipart form data." });
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.BadRequest(new { error = "Choose a .docx file to upload." });
            if (file.Length > MaxUploadBytes) return Results.BadRequest(new { error = "File is larger than 25 MB." });

            var siteId = form["siteId"].ToString().Trim();
            if (string.IsNullOrEmpty(siteId)) return Results.BadRequest(new { error = "Choose the client this document is for." });
            if (registry.Find(siteId) is null) return Results.BadRequest(new { error = $"Unknown client '{siteId}'." });

            var importAnyway = string.Equals(form["importAnyway"], "true", StringComparison.OrdinalIgnoreCase);
            var mayOverride = principal.HasClaim("perm", PermissionKeys.ArticleImportUnverified);
            var userId = Guid.Parse(principal.FindFirstValue("sub")!);

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            try
            {
                var outcome = await ingestion.IngestAsync(bytes, file.FileName, siteId, userId, importAnyway, mayOverride,
                                                          http.Connection.RemoteIpAddress?.ToString(), ct);
                var dto = ToDetail(outcome.Document, outcome.Candidates, outcome.Report, outcome.Duplicate, outcome.Images ?? []);
                return outcome.Duplicate ? Results.Ok(dto) : Results.Created($"/api/documents/{outcome.Document.Id}", dto);
            }
            catch (DocxFormatException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (OverrideNotPermittedException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        });

        group.MapGet("/", async (bool? showHidden, SwanDbContext db, CancellationToken ct) =>
        {
            var query = db.ArticleDocuments.AsNoTracking();
            if (showHidden != true) query = query.Where(d => !d.IsHidden);
            var rows = await query
                .OrderByDescending(d => d.UploadedUtc)
                .Select(d => new
                {
                    d.Id,
                    d.FileName,
                    Status = d.Status.ToString(),
                    d.UploadedUtc,
                    d.FileSizeBytes,
                    clientSiteId = d.SiteId,
                    isHidden = d.IsHidden,
                    Candidates = db.ArticleCandidates.Count(c => c.DocumentId == d.Id),
                    acceptedArticles = db.Articles.Count(a => a.SourceDocumentId == d.Id),
                })
                .ToListAsync(ct);
            return Results.Ok(rows);
        });

        // Hide: reversible, removes the document from the default list. Nothing is deleted.
        group.MapPost("/{id:guid}/hide", async (Guid id, SwanDbContext db, ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
        {
            var doc = await db.ArticleDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return Results.NotFound(new { error = "Document not found." });
            if (doc.IsHidden) return Results.Ok(new { id, isHidden = true });
            doc.IsHidden = true;
            doc.HiddenUtc = DateTime.UtcNow;
            await Audit(db, principal, http, "document.hidden", id, $"file={doc.FileName}", ct);
            return Results.Ok(new { id, isHidden = true });
        });

        group.MapPost("/{id:guid}/restore", async (Guid id, SwanDbContext db, ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
        {
            var doc = await db.ArticleDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return Results.NotFound(new { error = "Document not found." });
            if (!doc.IsHidden) return Results.Ok(new { id, isHidden = false });
            doc.IsHidden = false;
            doc.HiddenUtc = null;
            await Audit(db, principal, http, "document.restored", id, $"file={doc.FileName}", ct);
            return Results.Ok(new { id, isHidden = false });
        });

        // Permanent delete: only for a hidden document with no accepted articles. The original file and its
        // proposals are removed; pictures shared with other content are kept. Audited.
        group.MapDelete("/{id:guid}/permanent", async (Guid id, SwanDbContext db, LocalFileStore files,
                                                       ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
        {
            var doc = await db.ArticleDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return Results.NotFound(new { error = "Document not found." });
            if (!doc.IsHidden)
                return Results.Json(new { error = "Hide the document first. Only hidden documents can be deleted permanently." },
                                    statusCode: StatusCodes.Status409Conflict);
            if (await db.Articles.AnyAsync(a => a.SourceDocumentId == id, ct))
                return Results.Json(new { error = "This document has accepted articles, so it cannot be deleted. Those articles may have drafts on client sites." },
                                    statusCode: StatusCodes.Status409Conflict);

            var candidateIds = db.ArticleCandidates.Where(c => c.DocumentId == id).Select(c => c.Id);
            db.ArticleImages.RemoveRange(db.ArticleImages.Where(i => candidateIds.Contains(i.CandidateId)));
            db.ArticleCandidates.RemoveRange(db.ArticleCandidates.Where(c => c.DocumentId == id));
            db.ArticleDocuments.Remove(doc);
            await Audit(db, principal, http, "document.deleted_permanently", id, $"file={doc.FileName} hash={doc.ContentHash}", ct);

            // The original is keyed by its hash, which is unique, so no other record can share this file.
            files.Delete(doc.StorageKey);
            return Results.NoContent();
        });

        group.MapGet("/{id:guid}", async (Guid id, DocumentIngestion ingestion, SwanDbContext db, CancellationToken ct) =>
        {
            var doc = await db.ArticleDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return Results.NotFound(new { error = "Document not found." });
            var (candidates, report, images) = await ingestion.LoadAsync(doc, ct);
            var accepted = await db.Articles.AsNoTracking()
                .Where(a => a.SourceDocumentId == doc.Id)
                .ToDictionaryAsync(a => a.SourceCandidateId, a => a.Id, ct);
            return Results.Ok(ToDetail(doc, candidates, report, duplicate: false, images, accepted));
        });

        // Sets the client on a document uploaded before clients were chosen at upload. Once set, it cannot change.
        // Articles already accepted from the document take the same client.
        group.MapPut("/{id:guid}/client", async (Guid id, SetDocumentClientBody body, SiteRegistry registry, SwanDbContext db,
                                                 ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
        {
            var siteId = body.SiteId?.Trim() ?? "";
            if (siteId.Length == 0) return Results.BadRequest(new { error = "Choose the client this document is for." });
            if (registry.Find(siteId) is null) return Results.BadRequest(new { error = $"Unknown client '{siteId}'." });

            var doc = await db.ArticleDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return Results.NotFound(new { error = "Document not found." });
            if (doc.SiteId is not null)
                return Results.Json(new { error = $"This document is already set to client '{doc.SiteId}'. The client cannot be changed." },
                                    statusCode: StatusCodes.Status409Conflict);

            doc.SiteId = siteId;
            var articles = await db.Articles.Where(a => a.SourceDocumentId == id && a.SiteId == null).ToListAsync(ct);
            foreach (var a in articles) a.SiteId = siteId;

            db.AuditLogs.Add(new Swan.Domain.Identity.AuditLog
            {
                UserId = Guid.Parse(principal.FindFirstValue("sub")!),
                Action = "document.client_set",
                EntityType = "ArticleDocument",
                EntityId = id.ToString(),
                Details = $"client={siteId} articles={articles.Count}",
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
                CreatedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { clientSiteId = siteId, articlesUpdated = articles.Count });
        });

        // The house-style template, so writers can start from a conforming file (PLAN-v2 §6.4).
        group.MapGet("/template", () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Swan-Article-Template.docx");
            return File.Exists(path)
                ? Results.File(path, "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                               fileDownloadName: "Swan-Article-Template.docx")
                : Results.NotFound(new { error = "Template file is missing from the server." });
        });
    }

    /// <summary>Writes an audit entry and saves all pending changes, so the action and its record change together.</summary>
    private static async Task Audit(SwanDbContext db, ClaimsPrincipal principal, HttpContext http, string action, Guid id,
                                    string details, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = Guid.Parse(principal.FindFirstValue("sub")!),
            Action = action,
            EntityType = "ArticleDocument",
            EntityId = id.ToString(),
            Details = details,
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Shape shared by document and article views. Problem is null when the picture can be sent.</summary>
    public static object ToImageDto(ArticleImage i) => new
    {
        ordinal = i.Ordinal,
        token = i.Token,
        paragraph = i.ParagraphIndex,
        fileName = i.FileName,
        mimeType = i.MimeType,
        altText = i.AltText,
        problem = i.Problem,
        ok = i.Problem is null,
    };

    private static object ToDetail(ArticleDocument doc, IReadOnlyList<ArticleCandidate> candidates,
                                   ConversionResult report, bool duplicate,
                                   IReadOnlyList<ArticleImage> images,
                                   IReadOnlyDictionary<Guid, Guid>? acceptedArticles = null) => new
    {
        id = doc.Id,
        fileName = doc.FileName,
        status = doc.Status.ToString(),
        uploadedUtc = doc.UploadedUtc,
        fileSizeBytes = doc.FileSizeBytes,
        clientSiteId = doc.SiteId,
        isHidden = doc.IsHidden,
        totalParagraphs = doc.TotalParagraphs,
        importedWithOverride = doc.ImportedWithOverride,
        duplicate,
        report,
        candidates = candidates.Select(c => new
        {
            id = c.Id,
            index = c.CandidateIndex,
            paragraphFrom = c.ParagraphFrom,
            paragraphTo = c.ParagraphTo,
            title = c.ProposedTitle,
            wordCount = c.WordCount,
            isUnverified = c.IsUnverified,
            state = c.State,
            acceptedArticleId = acceptedArticles is not null && acceptedArticles.TryGetValue(c.Id, out var aid) ? aid : (Guid?)null,
            images = images.Where(i => i.CandidateId == c.Id).Select(ToImageDto),
            contentHtml = c.ContentHtml,
            seo = new
            {
                seoTitle = c.SeoTitle,
                metaDescription = c.MetaDescription,
                canonicalPath = c.CanonicalPath,
                slug = c.Slug,
                warnings = string.IsNullOrEmpty(c.SeoWarnings)
                    ? Array.Empty<string>()
                    : c.SeoWarnings.Split('\n'),
            },
        }),
    };
}
