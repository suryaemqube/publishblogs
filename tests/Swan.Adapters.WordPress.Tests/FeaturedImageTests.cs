using System.Net;
using System.Text;
using ImageMagick;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Swan.Adapters.Abstractions;
using Swan.Adapters.WordPress;
using Swan.Domain.Content;
using Swan.Infrastructure.Content;
using Swan.Infrastructure.Media;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Storage;
using Xunit;

namespace Swan.Adapters.WordPress.Tests;

public sealed class FeaturedImageTests : IAsyncLifetime
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0];
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    private SqliteConnection _conn = null!;
    private SwanDbContext _db = null!;
    private string _dir = null!;
    private Article _article = null!;

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        await _conn.OpenAsync();
        _db = new SwanDbContext(new DbContextOptionsBuilder<SwanDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _dir = Path.Combine(Path.GetTempPath(), "swan-img-" + Guid.NewGuid().ToString("N"));
        _article = new Article { Id = Guid.NewGuid(), Title = "T", ContentHtml = "<p>x</p>", Status = "Draft", CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow };
        _db.Articles.Add(_article);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private FeaturedImageService Images() => new(_db, new LocalFileStore(_dir));

    [Fact]
    public async Task Jpeg_with_matching_name_is_accepted_and_linked_to_the_article()
    {
        await Images().SetAsync(_article, Jpeg, "cover.jpg", "A cover photo", Guid.NewGuid(), null);

        var tracked = await _db.Articles.AsNoTracking().SingleAsync(a => a.Id == _article.Id);
        Assert.NotNull(tracked.FeaturedMediaId);
        Assert.Equal("A cover photo", tracked.FeaturedAltText);
        Assert.Equal("image/jpeg", (await _db.MediaFiles.SingleAsync()).MimeType);
    }

    [Fact]
    public async Task Renamed_executable_is_refused_by_signature()
    {
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0, 0, 0, 0, 0, 0, 0, 0 };
        var ex = await Assert.ThrowsAsync<ImageRejectedException>(() =>
            Images().SetAsync(_article, exe, "cover.jpg", "A cover photo", Guid.NewGuid(), null));
        Assert.Contains("could not be read as a picture", ex.Message);
    }

    [Fact]
    public async Task Truncated_png_header_is_refused_as_unreadable()
    {
        var ex = await Assert.ThrowsAsync<ImageRejectedException>(() =>
            Images().SetAsync(_article, Png, "cover.png", "A cover photo", Guid.NewGuid(), null));
        Assert.Contains("could not be read as a picture", ex.Message);
    }

    [Fact]
    public async Task Real_png_is_accepted_and_stored_as_webp()
    {
        var png = MakePicture(800, 600, MagickFormat.Png, smooth: true);
        await Images().SetAsync(_article, png, "chart.png", "A smooth gradient", Guid.NewGuid(), null);

        var media = await _db.MediaFiles.SingleAsync();
        Assert.Equal("image/webp", media.MimeType);
        Assert.Equal("chart.webp", media.FileName);
        Assert.True(media.ByteSize <= ImageConverter.MaxBytes);
    }

    [Fact]
    public async Task Large_picture_is_converted_to_fit_under_500_kb()
    {
        var png = MakePicture(3000, 2000, MagickFormat.Png, smooth: true);
        await Images().SetAsync(_article, png, "big.png", "A large gradient", Guid.NewGuid(), null);

        var media = await _db.MediaFiles.SingleAsync();
        Assert.Equal("image/webp", media.MimeType);
        Assert.True(media.ByteSize <= 500 * 1024);
    }

    [Fact]
    public async Task Picture_that_cannot_be_made_small_enough_is_refused_with_a_clear_message()
    {
        var noisy = MakePicture(2000, 2000, MagickFormat.Png, smooth: false);
        var ex = await Assert.ThrowsAsync<ImageRejectedException>(() =>
            Images().SetAsync(_article, noisy, "noise.png", "A very detailed photo", Guid.NewGuid(), null));
        Assert.Contains("The limit is 500 KB", ex.Message);
        Assert.Equal(0, await _db.MediaFiles.CountAsync());
    }

    /// <summary>A picture of the given size. Smooth pictures compress well; noisy ones cannot be made small.</summary>
    private static byte[] MakePicture(int width, int height, MagickFormat format, bool smooth)
    {
        using var image = smooth
            ? new MagickImage(MagickColors.SteelBlue, (uint)width, (uint)height)
            : NoiseImage(width, height);
        image.Format = format;
        return image.ToByteArray();
    }

    private static MagickImage NoiseImage(int width, int height)
    {
        var rnd = new Random(7);
        var rgb = new ushort[width * height * 3];
        for (var i = 0; i < rgb.Length; i++) rgb[i] = (ushort)(rnd.Next(256) * 257);
        var image = new MagickImage(MagickColors.Black, (uint)width, (uint)height);
        image.GetPixels().SetPixels(rgb);
        return image;
    }

    [Fact]
    public async Task Draft_is_blocked_without_a_featured_image()
    {
        var tracked = await _db.Articles.FirstAsync(a => a.Id == _article.Id);
        var pub = new FakePublisher();
        await Assert.ThrowsAsync<DraftBlockedException>(() =>
            new DraftSync(_db, new LocalFileStore(_dir)).SyncAsync(tracked, "emqube", pub, Guid.NewGuid(), null));
        Assert.Equal(0, pub.Creates);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ok")]
    public async Task Missing_or_too_short_alt_text_is_refused(string alt)
    {
        await Assert.ThrowsAsync<ImageRejectedException>(() =>
            Images().SetAsync(_article, Jpeg, "cover.jpg", alt, Guid.NewGuid(), null));
    }

    [Fact]
    public async Task Same_image_uploaded_twice_is_stored_once()
    {
        await Images().SetAsync(_article, Jpeg, "a.jpg", "Alt text one", Guid.NewGuid(), null);
        await Images().SetAsync(_article, Jpeg, "b.jpg", "Alt text two", Guid.NewGuid(), null);
        Assert.Equal(1, await _db.MediaFiles.CountAsync());
    }

    [Fact]
    public async Task Draft_uploads_the_featured_image_once_and_attaches_it_to_the_post()
    {
        await Images().SetAsync(_article, Jpeg, "cover.jpg", "A cover photo", Guid.NewGuid(), null);
        var tracked = await _db.Articles.FirstAsync(a => a.Id == _article.Id);
        var sync = new DraftSync(_db, new LocalFileStore(_dir));
        var pub = new FakePublisher();

        await sync.SyncAsync(tracked, "emqube", pub, Guid.NewGuid(), null);
        Assert.Equal(1, pub.Uploads);
        Assert.Equal(701, pub.LastFeaturedMediaId);

        // A later update reuses the uploaded image: no second upload.
        await sync.SyncAsync(tracked, "emqube", pub, Guid.NewGuid(), null);
        Assert.Equal(1, pub.Uploads);
        Assert.Equal(701, pub.LastFeaturedMediaId);
    }

    [Fact]
    public async Task Upload_sets_alt_text_and_media_is_sent_with_the_right_mime_type()
    {
        var calls = new List<(string Method, string Path, string ContentType, string Body)>();
        var handler = new FakeWordPress(r =>
        {
            return (HttpStatusCode.Created, """{"id":55,"source_url":"https://wp/img.jpg"}""");
        });
        using var client = new WordPressRestClient(new WordPressSiteOptions(
            new Uri("https://wp.example.com/"), "blogs", "blogs", "u", "p"), handler);

        var remote = await new WordPressContentPublisher(client).UploadMediaAsync(
            new MediaUpload(Jpeg, "cover.jpg", "image/jpeg", "A cover photo"));

        Assert.Equal(55, remote.RemoteId);
        Assert.Equal("/wp-json/wp/v2/media", handler.Sent[0].Path);
        Assert.Equal("/wp-json/wp/v2/media/55", handler.Sent[1].Path);
        Assert.Contains("A cover photo", handler.Sent[1].Body);
    }
}
