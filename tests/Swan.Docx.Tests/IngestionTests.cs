using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Swan.Docx;
using Swan.Domain.Content;
using Swan.Infrastructure.Content;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Storage;
using Xunit;

namespace Swan.Docx.Tests;

public sealed class IngestionTests : IAsyncLifetime
{
    private SqliteConnection _conn = null!;
    private SwanDbContext _db = null!;
    private string _storeDir = null!;
    private static readonly Guid Writer = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        await _conn.OpenAsync();
        _db = new SwanDbContext(new DbContextOptionsBuilder<SwanDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _storeDir = Path.Combine(Path.GetTempPath(), "swan-ingest-" + Guid.NewGuid().ToString("N"));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
        if (Directory.Exists(_storeDir)) Directory.Delete(_storeDir, recursive: true);
    }

    private DocumentIngestion Ingestion() => new(_db, new LocalFileStore(_storeDir));

    private static byte[] Fixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public async Task Document_is_stored_with_the_client_it_was_uploaded_for()
    {
        var outcome = await Ingestion().IngestAsync(Fixture("Swan-Article-Template.docx"), "template.docx", "emoversuae", Writer, false, false, null);

        Assert.Equal("emoversuae", outcome.Document.SiteId);
    }

    [Fact]
    public async Task Upload_without_a_client_is_refused()
    {
        await Assert.ThrowsAsync<DocxFormatException>(() =>
            Ingestion().IngestAsync(Fixture("Swan-Article-Template.docx"), "template.docx", "  ", Writer, false, false, null));
    }

    [Fact]
    public async Task Template_upload_conforms_and_proposes_four_articles()
    {
        var outcome = await Ingestion().IngestAsync(Fixture("Swan-Article-Template.docx"), "template.docx", "emqube", Writer, false, false, null);

        Assert.Equal(DocumentStatus.Conforming, outcome.Document.Status);
        Assert.False(outcome.Duplicate);
        Assert.Equal(4, outcome.Candidates.Count);
        Assert.All(outcome.Candidates, c => Assert.False(c.IsUnverified));
        Assert.True(File.Exists(Path.Combine(_storeDir, outcome.Document.StorageKey)));
    }

    [Fact]
    public async Task Non_conforming_upload_is_recorded_as_rejected_with_no_articles()
    {
        var outcome = await Ingestion().IngestAsync(Fixture("Emqube Blogs Oct 2026.docx"), "Emqube Blogs Oct 2026.docx", "emqube", Writer, false, false, null);

        Assert.Equal(DocumentStatus.Rejected, outcome.Document.Status);
        Assert.Empty(outcome.Candidates);
        Assert.NotEmpty(outcome.Report.RejectReasons);
        Assert.Single(await _db.ArticleDocuments.ToListAsync());
    }

    [Fact]
    public async Task Same_file_twice_is_detected_as_duplicate_and_not_stored_twice()
    {
        var bytes = Fixture("Swan-Article-Template.docx");
        await Ingestion().IngestAsync(bytes, "a.docx", "emqube", Writer, false, false, null);
        var second = await Ingestion().IngestAsync(bytes, "b.docx", "emqube", Writer, false, false, null);

        Assert.True(second.Duplicate);
        Assert.Equal(1, await _db.ArticleDocuments.CountAsync());
        Assert.Equal(4, second.Candidates.Count);
    }

    [Fact]
    public async Task Override_without_permission_is_refused()
    {
        await Assert.ThrowsAsync<OverrideNotPermittedException>(() =>
            Ingestion().IngestAsync(Fixture("Emqube Blogs Oct 2026.docx"), "x.docx", "emqube", Writer, importAnyway: true, userMayImportUnverified: false, null));
    }

    [Fact]
    public async Task Override_with_permission_imports_one_unverified_article_and_is_audited()
    {
        var outcome = await Ingestion().IngestAsync(Fixture("Emqube Blogs Oct 2026.docx"), "Emqube Blogs Oct 2026.docx", "emqube", Writer, importAnyway: true, userMayImportUnverified: true, null);

        Assert.Equal(DocumentStatus.ImportedUnverified, outcome.Document.Status);
        var only = Assert.Single(outcome.Candidates);
        Assert.True(only.IsUnverified);
        Assert.Equal("Emqube Blogs Oct 2026", only.ProposedTitle);
        Assert.Contains(await _db.AuditLogs.ToListAsync(), a => a.Action == "document.imported_unverified");
    }

    [Fact]
    public async Task Non_docx_file_name_is_refused()
    {
        await Assert.ThrowsAsync<DocxFormatException>(() =>
            Ingestion().IngestAsync(Fixture("Swan-Article-Template.docx"), "article.pdf", "emqube", Writer, false, false, null));
    }
}
