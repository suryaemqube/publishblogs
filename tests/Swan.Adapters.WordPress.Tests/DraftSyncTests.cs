using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Swan.Adapters.Abstractions;
using Swan.Adapters.WordPress;
using Swan.Domain.Content;
using Swan.Infrastructure.Content;
using Swan.Infrastructure.Persistence;
using Xunit;

namespace Swan.Adapters.WordPress.Tests;

/// <summary>Fake publisher that records calls and reports a configurable remote status.</summary>
internal sealed class FakePublisher : IContentPublisher
{
    public string RemoteStatus { get; set; } = "draft";
    public bool Exists { get; set; } = true;
    public int Creates { get; private set; }
    public int Updates { get; private set; }

    public string LastContent { get; private set; } = "";

    public Task<RemoteDraftResult> CreateDraftAsync(DraftRequest request, CancellationToken ct = default)
    {
        Creates++;
        LastFeaturedMediaId = request.FeaturedMediaId;
        LastContent = request.ContentHtml;
        return Task.FromResult(new RemoteDraftResult(4242, "draft", "https://x/?p=4242", "https://x/wp-admin/post.php?post=4242&action=edit"));
    }

    public Task<RemoteDraftResult> UpdateDraftAsync(long remoteId, DraftRequest request, CancellationToken ct = default)
    {
        Updates++;
        return Task.FromResult(new RemoteDraftResult(remoteId, "draft", "https://x/?p=" + remoteId, "https://x/wp-admin/post.php?post=" + remoteId + "&action=edit"));
    }

    public Task<RemotePostInfo?> GetPostAsync(long remoteId, CancellationToken ct = default) =>
        Task.FromResult<RemotePostInfo?>(Exists ? new RemotePostInfo(remoteId, RemoteStatus, null, null) : null);

    public int Uploads { get; private set; }
    public long? LastFeaturedMediaId { get; private set; }

    public Task<RemoteMedia> UploadMediaAsync(MediaUpload upload, CancellationToken ct = default)
    {
        Uploads++;
        return Task.FromResult(new RemoteMedia(700 + Uploads, "https://x/img.jpg"));
    }
}

public sealed class DraftSyncTests : IAsyncLifetime
{
    private SqliteConnection _conn = null!;
    private SwanDbContext _db = null!;
    private Article _article = null!;

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        await _conn.OpenAsync();
        _db = new SwanDbContext(new DbContextOptionsBuilder<SwanDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        // Drafts need a featured image (client rule). Store a real file so the sync can upload it.
        var dir = Path.Combine(Path.GetTempPath(), "swan-sync-tests");
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, "sync-test.jpg"), [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4]);
        var media = new MediaFile
        {
            Id = Guid.NewGuid(), ContentHash = Guid.NewGuid().ToString("N"), StorageKey = "sync-test.jpg",
            FileName = "cover.jpg", MimeType = "image/jpeg", ByteSize = 8, UploadedUtc = DateTime.UtcNow,
        };
        _db.MediaFiles.Add(media);

        _article = new Article
        {
            Id = Guid.NewGuid(), Title = "AI guide", ContentHtml = "<p>x</p>", Slug = "ai-guide",
            Status = "Draft", FeaturedMediaId = media.Id, FeaturedAltText = "A cover photo",
            CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow,
        };
        _db.Articles.Add(_article);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private DraftSync NewSync() => new(_db, new Swan.Infrastructure.Storage.LocalFileStore(Path.Combine(Path.GetTempPath(), "swan-sync-tests")));

    [Fact]
    public async Task First_send_creates_a_draft_and_remembers_its_id()
    {
        var pub = new FakePublisher();
        var outcome = await NewSync().SyncAsync(_article, "emqube", pub, Guid.NewGuid(), null);

        Assert.Equal(DraftSyncAction.Created, outcome.Action);
        Assert.Equal(1, pub.Creates);
        Assert.Equal(4242, (await _db.ArticlePublications.SingleAsync()).RemotePostId);
    }

    [Fact]
    public async Task Second_send_updates_the_same_draft_and_creates_no_duplicate()
    {
        var sync = NewSync();
        var pub = new FakePublisher();
        await sync.SyncAsync(_article, "emqube", pub, Guid.NewGuid(), null);
        var second = await sync.SyncAsync(_article, "emqube", pub, Guid.NewGuid(), null);

        Assert.Equal(DraftSyncAction.Updated, second.Action);
        Assert.Equal(1, pub.Creates);
        Assert.Equal(1, pub.Updates);
        Assert.Equal(1, await _db.ArticlePublications.CountAsync());
    }

    [Fact]
    public async Task Published_post_is_refused_and_never_updated()
    {
        var sync = NewSync();
        var pub = new FakePublisher();
        await sync.SyncAsync(_article, "emqube", pub, Guid.NewGuid(), null);

        pub.RemoteStatus = "publish";
        await Assert.ThrowsAsync<RemoteDraftPublishedException>(() =>
            sync.SyncAsync(_article, "emqube", pub, Guid.NewGuid(), null));
        Assert.Equal(0, pub.Updates);
    }

    [Fact]
    public async Task Deleted_remote_post_is_refused_rather_than_recreated_silently()
    {
        var sync = NewSync();
        var pub = new FakePublisher();
        await sync.SyncAsync(_article, "emqube", pub, Guid.NewGuid(), null);

        pub.Exists = false;
        await Assert.ThrowsAsync<RemoteDraftMissingException>(() =>
            sync.SyncAsync(_article, "emqube", pub, Guid.NewGuid(), null));
        Assert.Equal(1, pub.Creates);
    }

    [Fact]
    public async Task Update_request_never_contains_a_status_field()
    {
        var sent = new List<string>();
        var handler = new FakeWordPress(r =>
        {
            if (r.Method == HttpMethod.Get) return (HttpStatusCode.OK, """{"id":9,"status":"draft","slug":"x","link":"l"}""");
            return (HttpStatusCode.OK, """{"id":9,"status":"draft","link":"l"}""");
        });
        using var client = new WordPressRestClient(new WordPressSiteOptions(
            new Uri("https://wp.example.com/"), "blogs", "blogs", "u", "p"), handler);

        await new WordPressContentPublisher(client).UpdateDraftAsync(9, new DraftRequest("T", "<p>x</p>", Slug: "x"));

        var post = handler.Sent.Single(s => s.Method == HttpMethod.Post);
        Assert.Equal("/wp-json/wp/v2/blogs/9", post.Path);
        using var json = JsonDocument.Parse(post.Body);
        Assert.False(json.RootElement.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task Adapter_refuses_update_when_site_reports_published()
    {
        var handler = new FakeWordPress(r =>
            r.Method == HttpMethod.Get
                ? (HttpStatusCode.OK, """{"id":9,"status":"publish","link":"l"}""")
                : (HttpStatusCode.OK, """{"id":9,"status":"publish","link":"l"}"""));
        using var client = new WordPressRestClient(new WordPressSiteOptions(
            new Uri("https://wp.example.com/"), "blogs", "blogs", "u", "p"), handler);

        var ex = await Assert.ThrowsAsync<RemotePostNotDraftException>(() =>
            new WordPressContentPublisher(client).UpdateDraftAsync(9, new DraftRequest("T", "<p>x</p>")));
        Assert.Equal("publish", ex.Status);
        Assert.DoesNotContain(handler.Sent, s => s.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Yoast_fields_are_sent_only_when_enabled_for_the_site()
    {
        var handler = new FakeWordPress(_ => (HttpStatusCode.Created, """{"id":5,"status":"draft","link":"l"}"""));
        using var client = new WordPressRestClient(new WordPressSiteOptions(
            new Uri("https://wp.example.com/"), "blogs", "blogs", "u", "p"), handler);
        var request = new DraftRequest("T", "<p>x</p>", SeoTitle: "SEO", MetaDescription: "Desc");

        await new WordPressContentPublisher(client, writeYoastMeta: false).CreateDraftAsync(request);
        Assert.DoesNotContain("_yoast_wpseo_title", handler.Sent.Single().Body);

        handler.Sent.Clear();
        await new WordPressContentPublisher(client, writeYoastMeta: true).CreateDraftAsync(request);
        Assert.Contains("_yoast_wpseo_title", handler.Sent.Single().Body);
        Assert.Contains("_yoast_wpseo_metadesc", handler.Sent.Single().Body);
    }

    [Fact]
    public async Task Inline_picture_is_uploaded_and_its_token_replaced_with_the_site_address()
    {
        // A different file from the featured image, so it is uploaded separately.
        var dir = Path.Combine(Path.GetTempPath(), "swan-sync-tests");
        await File.WriteAllBytesAsync(Path.Combine(dir, "team-test.jpg"), [0xFF, 0xD8, 0xFF, 0xE0, 9, 9, 9, 9]);
        var media = new MediaFile
        {
            Id = Guid.NewGuid(), ContentHash = Guid.NewGuid().ToString("N"), StorageKey = "team-test.jpg",
            FileName = "team.jpg", MimeType = "image/jpeg", ByteSize = 8, UploadedUtc = DateTime.UtcNow,
        };
        _db.MediaFiles.Add(media);
        _article.SourceCandidateId = Guid.NewGuid();
        _article.ContentHtml = "<figure><img src=\"swan-image:1\" alt=\"A team\" /></figure>";
        _db.ArticleImages.Add(new ArticleImage
        {
            Id = Guid.NewGuid(), CandidateId = _article.SourceCandidateId, Ordinal = 1, Token = "swan-image:1",
            ParagraphIndex = 3, MediaFileId = media.Id, FileName = "team.jpg", MimeType = "image/jpeg", AltText = "A team",
        });
        await _db.SaveChangesAsync();

        var pub = new FakePublisher();
        await NewSync().SyncAsync(_article, "emqube", pub, Guid.NewGuid(), null);

        Assert.Equal(2, pub.Uploads); // featured image and the inline picture
        Assert.Contains("src=\"https://x/img.jpg\"", pub.LastContent);
        Assert.DoesNotContain("swan-image:1", pub.LastContent);
    }

    [Fact]
    public async Task Article_with_a_broken_picture_is_not_sent_at_all()
    {
        _article.SourceCandidateId = Guid.NewGuid();
        _db.ArticleImages.Add(new ArticleImage
        {
            Id = Guid.NewGuid(), CandidateId = _article.SourceCandidateId, Ordinal = 1, Token = "swan-image:1",
            ParagraphIndex = 3, FileName = "chart.png", AltText = "A chart",
            Problem = "Image 1 (paragraph 3) is a PNG. Save it as JPG or WebP and reinsert it.",
        });
        await _db.SaveChangesAsync();

        var pub = new FakePublisher();
        var ex = await Assert.ThrowsAsync<DraftBlockedException>(() =>
            NewSync().SyncAsync(_article, "emqube", pub, Guid.NewGuid(), null));
        Assert.Contains("PNG", ex.Message);
        Assert.Equal(0, pub.Creates);
        Assert.Equal(0, pub.Uploads);
    }

    [Fact]
    public async Task Accepting_the_same_proposal_twice_creates_one_article()
    {
        var doc = new ArticleDocument
        {
            Id = Guid.NewGuid(), FileName = "a.docx", ContentHash = "h", StorageKey = "h.docx",
            Status = DocumentStatus.Conforming, ReportJson = "{}", UploadedUtc = DateTime.UtcNow,
        };
        var cand = new ArticleCandidate
        {
            Id = Guid.NewGuid(), DocumentId = doc.Id, CandidateIndex = 1, ParagraphFrom = 1, ParagraphTo = 2,
            ProposedTitle = "Proposed", ContentHtml = "<p>x</p>", WordCount = 1, State = "Proposed",
        };
        _db.ArticleDocuments.Add(doc);
        _db.ArticleCandidates.Add(cand);
        await _db.SaveChangesAsync();

        var service = new ArticleService(_db);
        var first = await service.AcceptCandidateAsync(cand.Id, Guid.NewGuid(), null);
        var second = await service.AcceptCandidateAsync(cand.Id, Guid.NewGuid(), null);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, await _db.Articles.CountAsync()); // the setup article plus the accepted one
    }
}

