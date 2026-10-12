using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Swan.Adapters.Abstractions;
using Swan.Adapters.WordPress;
using Xunit;

namespace Swan.Adapters.WordPress.Tests;

/// <summary>Routes requests to canned responses. Records every request so tests can assert on what was sent.</summary>
internal sealed class FakeWordPress(Func<HttpRequestMessage, (HttpStatusCode, string)> route) : HttpMessageHandler
{
    public List<(HttpMethod Method, string Path, string Body)> Sent { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        Sent.Add((request.Method, request.RequestUri!.PathAndQuery, body));
        var (status, json) = route(request);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }
}

public class WordPressAdapterTests
{
    private static readonly Uri Origin = new("https://wp.example.com/");

    private static WordPressSiteOptions Options(string postType = "blogs", string restBase = "blogs") =>
        new(Origin, postType, restBase, "writer", "abcd efgh ijkl mnop");

    private static WordPressRestClient Client(FakeWordPress fake, WordPressSiteOptions? opts = null) =>
        new(opts ?? Options(), fake);

    [Fact]
    public async Task Basic_auth_header_is_sent_with_application_password()
    {
        AuthenticationHeaderValue? seen = null;
        var fake = new FakeWordPress(r =>
        {
            seen = r.Headers.Authorization;
            return (HttpStatusCode.OK, "{}");
        });
        using var client = Client(fake);

        await client.GetAsync("/wp-json/");

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("writer:abcd efgh ijkl mnop"));
        Assert.NotNull(seen);
        Assert.Equal("Basic", seen!.Scheme);
        Assert.Equal(expected, seen.Parameter);
    }

    [Fact]
    public async Task Draft_is_created_with_status_draft_and_returns_edit_url()
    {
        var fake = new FakeWordPress(_ => (HttpStatusCode.Created,
            """{"id":4521,"status":"draft","link":"https://wp.example.com/?p=4521"}"""));
        using var client = Client(fake);
        var publisher = new WordPressContentPublisher(client);

        var result = await publisher.CreateDraftAsync(new DraftRequest("Hello", "<p>x</p>"));

        Assert.Equal(4521, result.RemoteId);
        Assert.Equal("draft", result.Status);
        Assert.Equal("https://wp.example.com/wp-admin/post.php?post=4521&action=edit", result.EditUrl);

        var sent = Assert.Single(fake.Sent);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("/wp-json/wp/v2/blogs", sent.Path);
        using var json = JsonDocument.Parse(sent.Body);
        Assert.Equal("draft", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Draft_fails_loudly_when_site_returns_a_non_draft_status()
    {
        var fake = new FakeWordPress(_ => (HttpStatusCode.Created,
            """{"id":9,"status":"publish","link":"x"}"""));
        using var client = Client(fake);

        var ex = await Assert.ThrowsAsync<WordPressApiException>(() =>
            new WordPressContentPublisher(client).CreateDraftAsync(new DraftRequest("T", "<p>x</p>")));
        Assert.Contains("publish", ex.Message);
    }

    [Fact]
    public async Task Draft_surfaces_site_error_code()
    {
        var fake = new FakeWordPress(_ => (HttpStatusCode.Forbidden,
            """{"code":"rest_cannot_create","message":"Sorry, you are not allowed to create posts."}"""));
        using var client = Client(fake);

        var ex = await Assert.ThrowsAsync<WordPressApiException>(() =>
            new WordPressContentPublisher(client).CreateDraftAsync(new DraftRequest("T", "<p>x</p>")));
        Assert.Equal("rest_cannot_create", ex.Code);
    }

    [Fact]
    public async Task Connection_test_passes_on_a_healthy_site_and_checks_each_taxonomy()
    {
        var fake = new FakeWordPress(r => r.RequestUri!.AbsolutePath switch
        {
            "/wp-json/" => (HttpStatusCode.OK, """{"name":"Client","namespaces":["wp/v2"]}"""),
            "/wp-json/wp/v2/users/me" => (HttpStatusCode.OK, """{"name":"writer"}"""),
            "/wp-json/wp/v2/types/blogs" => (HttpStatusCode.OK,
                """{"rest_base":"blogs","taxonomies":["blog_cat"]}"""),
            "/wp-json/wp/v2/taxonomies/blog_cat" => (HttpStatusCode.OK, """{"rest_base":"blog_cat"}"""),
            _ => (HttpStatusCode.NotFound, """{"code":"rest_no_route"}"""),
        });
        using var client = Client(fake);

        var report = await new WordPressConnectionTest(client).RunAsync();

        Assert.True(report.Passed, string.Join("; ", report.Checks.Where(c => !c.Passed).Select(c => c.Name + ": " + c.Detail)));
        Assert.Contains(report.Checks, c => c.Name == "Taxonomy 'blog_cat'" && c.Passed);
    }

    [Fact]
    public async Task Yoast_meta_check_passes_when_the_keys_are_registered_for_rest()
    {
        var fake = new FakeWordPress(r => r.RequestUri!.AbsolutePath switch
        {
            "/wp-json/" => (HttpStatusCode.OK, """{"name":"Client","namespaces":["wp/v2"]}"""),
            "/wp-json/wp/v2/users/me" => (HttpStatusCode.OK, """{"name":"writer"}"""),
            "/wp-json/wp/v2/types/blogs" => (HttpStatusCode.OK, """{"rest_base":"blogs","taxonomies":[]}"""),
            "/wp-json/wp/v2/blogs" => (HttpStatusCode.OK,
                """{"schema":{"properties":{"meta":{"properties":{"_yoast_wpseo_title":{},"_yoast_wpseo_metadesc":{},"footnotes":{}}}}}}"""),
            _ => (HttpStatusCode.NotFound, """{"code":"rest_no_route"}"""),
        });
        using var client = Client(fake);

        var report = await new WordPressConnectionTest(client).RunAsync(default, WordPressContentPublisher.YoastMetaKeys);

        Assert.True(report.Passed, string.Join("; ", report.Checks.Where(c => !c.Passed).Select(c => c.Name + ": " + c.Detail)));
        Assert.Contains(report.Checks, c => c.Name == "Meta keys exposed over REST" && c.Passed);
    }

    [Fact]
    public async Task Yoast_meta_check_fails_and_names_the_keys_when_the_site_does_not_expose_them()
    {
        var fake = new FakeWordPress(r => r.RequestUri!.AbsolutePath switch
        {
            "/wp-json/" => (HttpStatusCode.OK, """{"name":"Client","namespaces":["wp/v2"]}"""),
            "/wp-json/wp/v2/users/me" => (HttpStatusCode.OK, """{"name":"writer"}"""),
            "/wp-json/wp/v2/types/blogs" => (HttpStatusCode.OK, """{"rest_base":"blogs","taxonomies":[]}"""),
            "/wp-json/wp/v2/blogs" => (HttpStatusCode.OK,
                """{"schema":{"properties":{"meta":{"properties":{"inline_featured_image":{},"footnotes":{}}}}}}"""),
            _ => (HttpStatusCode.NotFound, """{"code":"rest_no_route"}"""),
        });
        using var client = Client(fake);

        var report = await new WordPressConnectionTest(client).RunAsync(default, WordPressContentPublisher.YoastMetaKeys);

        Assert.False(report.Passed);
        var check = Assert.Single(report.Checks, c => c.Name == "Meta keys exposed over REST");
        Assert.False(check.Passed);
        Assert.Contains("_yoast_wpseo_title", check.Detail);
        Assert.Contains("_yoast_wpseo_metadesc", check.Detail);
    }

    [Fact]
    public async Task Connection_test_without_required_meta_keys_does_not_check_them()
    {
        var fake = new FakeWordPress(r => r.RequestUri!.AbsolutePath switch
        {
            "/wp-json/" => (HttpStatusCode.OK, """{"name":"Client","namespaces":["wp/v2"]}"""),
            "/wp-json/wp/v2/users/me" => (HttpStatusCode.OK, """{"name":"writer"}"""),
            "/wp-json/wp/v2/types/blogs" => (HttpStatusCode.OK, """{"rest_base":"blogs","taxonomies":[]}"""),
            _ => (HttpStatusCode.NotFound, """{"code":"rest_no_route"}"""),
        });
        using var client = Client(fake);

        var report = await new WordPressConnectionTest(client).RunAsync();

        Assert.DoesNotContain(report.Checks, c => c.Name == "Meta keys exposed over REST");
        Assert.True(report.Passed);
    }

    [Fact]
    public async Task Connection_test_fails_when_post_type_is_withheld_from_rest_with_401_not_404()
    {
        var fake = new FakeWordPress(r => r.RequestUri!.AbsolutePath switch
        {
            "/wp-json/" => (HttpStatusCode.OK, """{"name":"Client","namespaces":["wp/v2"]}"""),
            "/wp-json/wp/v2/users/me" => (HttpStatusCode.OK, """{"name":"writer"}"""),
            "/wp-json/wp/v2/types/move-on" => (HttpStatusCode.Unauthorized,
                """{"code":"rest_cannot_read_type","message":"Sorry, you are not allowed to read this post type."}"""),
            _ => (HttpStatusCode.NotFound, """{"code":"rest_no_route"}"""),
        });
        using var client = Client(fake, Options("move-on", "move-on"));

        var report = await new WordPressConnectionTest(client).RunAsync();

        Assert.False(report.Passed);
        var check = Assert.Single(report.Checks, c => c.Name == "Post type REST-exposed");
        Assert.False(check.Passed);
        Assert.Contains("rest_cannot_read_type", check.Detail);
    }

    [Fact]
    public async Task Connection_test_fails_when_rest_base_differs_from_configuration()
    {
        var fake = new FakeWordPress(r => r.RequestUri!.AbsolutePath switch
        {
            "/wp-json/" => (HttpStatusCode.OK, """{"name":"Client","namespaces":["wp/v2"]}"""),
            "/wp-json/wp/v2/users/me" => (HttpStatusCode.OK, """{"name":"writer"}"""),
            "/wp-json/wp/v2/types/blog" => (HttpStatusCode.OK, """{"rest_base":"blog","taxonomies":[]}"""),
            _ => (HttpStatusCode.NotFound, """{"code":"rest_no_route"}"""),
        });
        using var client = Client(fake, Options("blog", "blogs"));

        var report = await new WordPressConnectionTest(client).RunAsync();

        Assert.Contains(report.Checks, c => c.Name == "Post type REST-exposed" && !c.Passed && c.Detail.Contains("'blog'"));
    }

    [Fact]
    public async Task Connection_test_reports_failed_authentication()
    {
        var fake = new FakeWordPress(r => r.RequestUri!.AbsolutePath switch
        {
            "/wp-json/" => (HttpStatusCode.OK, """{"name":"Client","namespaces":["wp/v2"]}"""),
            "/wp-json/wp/v2/users/me" => (HttpStatusCode.Unauthorized,
                """{"code":"rest_not_logged_in","message":"You are not currently logged in."}"""),
            _ => (HttpStatusCode.NotFound, """{"code":"rest_no_route"}"""),
        });
        using var client = Client(fake);

        var report = await new WordPressConnectionTest(client).RunAsync();

        var auth = Assert.Single(report.Checks, c => c.Name == "Authentication");
        Assert.False(auth.Passed);
        Assert.Contains("rest_not_logged_in", auth.Detail);
    }

    [Theory]
    [InlineData("https://wp.example.com/blog", "blogs")]   // path not allowed on origin
    [InlineData("https://wp.example.com/", "")]            // rest base required
    public void Options_validation_rejects_bad_configuration(string origin, string restBase)
    {
        var opts = new WordPressSiteOptions(new Uri(origin), "blogs", restBase, "u", "p");
        Assert.Throws<ArgumentException>(() => opts.Validate());
    }

    [Fact]
    public void Options_validation_refuses_to_assume_post_type()
    {
        var opts = new WordPressSiteOptions(Origin, "", "blogs", "u", "p");
        Assert.Throws<ArgumentException>(() => opts.Validate());
    }

    [Fact]
    public async Task Unconfigured_publisher_throws_instead_of_silently_skipping()
    {
        await Assert.ThrowsAsync<PublisherNotConfiguredException>(() =>
            new NoOpContentPublisher().CreateDraftAsync(new DraftRequest("T", "<p>x</p>")));
    }
}
