using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swan.Docx;
using Swan.Domain.Content;
using Swan.Domain.Identity;
using Swan.Infrastructure.Content;
using Swan.Infrastructure.Persistence;

namespace Swan.Api.Documents;

public static class DocumentEndpoints
{
    private const long MaxUploadBytes = 25L * 1024 * 1024;

    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/documents").RequireAuthorization(PermissionKeys.ArticleWrite);

        // Upload. Returns 201 for a new file, 200 when the identical file was already uploaded.
        group.MapPost("/", async (HttpRequest request, DocumentIngestion ingestion, ClaimsPrincipal principal,
                                  HttpContext http, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(new { error = "Send the file as multipart form data." });
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.BadRequest(new { error = "Choose a .docx file to upload." });
            if (file.Length > MaxUploadBytes) return Results.BadRequest(new { error = "File is larger than 25 MB." });

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
                var outcome = await ingestion.IngestAsync(bytes, file.FileName, userId, importAnyway, mayOverride,
                                                          http.Connection.RemoteIpAddress?.ToString(), ct);
                var dto = ToDetail(outcome.Document, outcome.Candidates, outcome.Report, outcome.Duplicate);
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

        group.MapGet("/", async (SwanDbContext db, CancellationToken ct) =>
        {
            var rows = await db.ArticleDocuments.AsNoTracking()
                .OrderByDescending(d => d.UploadedUtc)
                .Select(d => new
                {
                    d.Id,
                    d.FileName,
                    Status = d.Status.ToString(),
                    d.UploadedUtc,
                    d.FileSizeBytes,
                    Candidates = db.ArticleCandidates.Count(c => c.DocumentId == d.Id),
                })
                .ToListAsync(ct);
            return Results.Ok(rows);
        });

        group.MapGet("/{id:guid}", async (Guid id, DocumentIngestion ingestion, SwanDbContext db, CancellationToken ct) =>
        {
            var doc = await db.ArticleDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return Results.NotFound(new { error = "Document not found." });
            var (candidates, report) = await ingestion.LoadAsync(doc, ct);
            return Results.Ok(ToDetail(doc, candidates, report, duplicate: false));
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

    private static object ToDetail(ArticleDocument doc, IReadOnlyList<ArticleCandidate> candidates,
                                   ConversionResult report, bool duplicate) => new
    {
        id = doc.Id,
        fileName = doc.FileName,
        status = doc.Status.ToString(),
        uploadedUtc = doc.UploadedUtc,
        fileSizeBytes = doc.FileSizeBytes,
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
            contentHtml = c.ContentHtml,
        }),
    };
}
