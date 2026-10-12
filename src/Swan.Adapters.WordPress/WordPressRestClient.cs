using System.Net;
using System.Net.Http.Headers;
// Content-Disposition and Content-Type headers come from System.Net.Http.Headers.
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Swan.Adapters.WordPress;

/// <summary>
/// Per-site WordPress settings. Nothing here is derived from the client's public domain:
/// RestOrigin is the address that serves /wp-json (PLAN-v2 §8.4).
/// </summary>
public sealed record WordPressSiteOptions(
    Uri RestOrigin,
    string PostTypeKey,
    string RestBase,
    string Username,
    string ApplicationPassword,
    string? UserAgent = null)
{
    public const string DefaultUserAgent = "SwanContentPublisher/0.1 (WordPress REST client)";

    public void Validate()
    {
        if (!RestOrigin.IsAbsoluteUri || RestOrigin.AbsolutePath != "/")
            throw new ArgumentException("RestOrigin must be an absolute origin with no path, e.g. https://wp.example.com");
        if (string.IsNullOrWhiteSpace(PostTypeKey)) throw new ArgumentException("PostTypeKey is required. Never assume 'post'.");
        if (string.IsNullOrWhiteSpace(RestBase)) throw new ArgumentException("RestBase is required.");
        if (string.IsNullOrWhiteSpace(Username)) throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(ApplicationPassword)) throw new ArgumentException("ApplicationPassword is required.");
    }
}

/// <summary>A WordPress REST response, already parsed. Never includes request credentials.</summary>
public sealed record WpResponse(HttpStatusCode Status, JsonNode? Body, string? ErrorCode, string? ErrorMessage)
{
    public bool Ok => (int)Status is >= 200 and < 300;
}

public sealed class WordPressApiException(string message, HttpStatusCode? status, string? code)
    : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
    public string? Code { get; } = code;
}

/// <summary>Thin HttpClient wrapper for the WordPress REST API, Application Password auth.</summary>
public sealed class WordPressRestClient : IDisposable
{
    private readonly HttpClient _http;

    public WordPressSiteOptions Options { get; }

    public WordPressRestClient(WordPressSiteOptions options, HttpMessageHandler? handler = null)
    {
        options.Validate();
        Options = options;

        // SocketsHttpHandler with a raised connection limit; avoids the ServicePointManager
        // defaults that produce false 404s on repeated requests (PLAN-v2 §8.4).
        // HTTP/2 preferred, HTTP/1.1 fallback. Some client hosts (SiteGround, emqube) block the
        // HTTP/1.1 request signature of this client with 403 while allowing HTTP/2 (verified 9 Oct 2026).
        _http = new HttpClient(handler ?? new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 64,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                     | System.Net.DecompressionMethods.Deflate
                                     | System.Net.DecompressionMethods.Brotli,
        })
        {
            BaseAddress = options.RestOrigin,
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestVersion = new Version(2, 0),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.ApplicationPassword}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Some client servers reject non-browser agents with 403. The value is per site (see SiteConfig.UserAgent).
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent ?? WordPressSiteOptions.DefaultUserAgent);
    }

    public Task<WpResponse> GetAsync(string path, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, path, null, ct);

    public Task<WpResponse> PostJsonAsync(string path, object body, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, path, body, ct);

    /// <summary>OPTIONS returns the endpoint schema, including the meta keys registered for REST. Read-only.</summary>
    public Task<WpResponse> OptionsAsync(string path, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Options, path, null, ct);

    /// <summary>Posts raw bytes (used for media uploads). The caller sets Content-Type and Content-Disposition.</summary>
    public async Task<WpResponse> PostBytesAsync(string path, byte[] bytes, string mimeType, string fileName, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Version = new Version(2, 0),
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            Content = new ByteArrayContent(bytes),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        request.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileName };

        using var response = await _http.SendAsync(request, ct);
        return await ParseAsync(response, ct);
    }

    private async Task<WpResponse> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        // HttpRequestMessage.Version defaults to 1.1 and overrides the client default, so set it per request.
        using var request = new HttpRequestMessage(method, path)
        {
            Version = new Version(2, 0),
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, ct);
        return await ParseAsync(response, ct);
    }

    private static async Task<WpResponse> ParseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);

        JsonNode? parsed = null;
        try { parsed = JsonNode.Parse(text); } catch (JsonException) { /* non-JSON body, e.g. HTML error page */ }

        return new WpResponse(
            response.StatusCode,
            parsed,
            parsed?["code"]?.ToString(),
            parsed?["message"]?.ToString());
    }

    public void Dispose() => _http.Dispose();
}
