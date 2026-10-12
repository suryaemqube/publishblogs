using System.Net;
using Swan.Adapters.Abstractions;

namespace Swan.Adapters.WordPress;

/// <summary>
/// Creates and updates WordPress drafts. Safety rules:
/// <list type="bullet">
/// <item>Creates always use status "draft". Nothing here can publish.</item>
/// <item>Updates read the post first and refuse unless it is a draft.</item>
/// <item>Updates never send a status field, so WordPress keeps whatever status the post has.</item>
/// </list>
/// </summary>
public sealed class WordPressContentPublisher(WordPressRestClient client, bool writeYoastMeta = false) : IContentPublisher
{
    // Yoast SEO meta keys. Writable over REST only after the client registers them with show_in_rest (see docs).
    public const string YoastTitleKey = "_yoast_wpseo_title";
    public const string YoastDescriptionKey = "_yoast_wpseo_metadesc";
    public static readonly string[] YoastMetaKeys = [YoastTitleKey, YoastDescriptionKey];

    public async Task<RemoteDraftResult> CreateDraftAsync(DraftRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentNullException.ThrowIfNull(request.ContentHtml);

        var payload = BuildPayload(request);
        payload["status"] = "draft";

        var path = $"/wp-json/wp/v2/{Uri.EscapeDataString(client.Options.RestBase)}";
        var res = await client.PostJsonAsync(path, payload, ct);

        if (!res.Ok || res.Body is null)
            throw new WordPressApiException(
                $"Draft creation failed: HTTP {(int)res.Status} {res.ErrorCode} {res.ErrorMessage}".TrimEnd(),
                res.Status, res.ErrorCode);

        var id = res.Body["id"]?.GetValue<long>() ?? throw new WordPressApiException("Response had no post id.", res.Status, null);
        var status = res.Body["status"]?.ToString() ?? "";
        if (status != "draft")
            throw new WordPressApiException($"Expected status 'draft' but site returned '{status}'. Investigate before continuing.", res.Status, null);

        return new RemoteDraftResult(id, status, res.Body["link"]?.ToString(), EditUrl(id));
    }

    public async Task<RemoteDraftResult> UpdateDraftAsync(long remoteId, DraftRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);

        var current = await GetPostAsync(remoteId, ct)
                      ?? throw new WordPressApiException($"Post {remoteId} no longer exists on the site.", HttpStatusCode.NotFound, "rest_post_invalid_id");

        if (current.Status != "draft")
            throw new RemotePostNotDraftException(remoteId, current.Status);

        var path = $"/wp-json/wp/v2/{Uri.EscapeDataString(client.Options.RestBase)}/{remoteId}";
        var res = await client.PostJsonAsync(path, BuildPayload(request), ct);

        if (!res.Ok || res.Body is null)
            throw new WordPressApiException(
                $"Draft update failed: HTTP {(int)res.Status} {res.ErrorCode} {res.ErrorMessage}".TrimEnd(),
                res.Status, res.ErrorCode);

        // If someone published the post between the read and the write, say so instead of pretending.
        var status = res.Body["status"]?.ToString() ?? "";
        if (status != "draft")
            throw new RemotePostNotDraftException(remoteId, status);

        return new RemoteDraftResult(remoteId, status, res.Body["link"]?.ToString(), EditUrl(remoteId));
    }

    public async Task<RemotePostInfo?> GetPostAsync(long remoteId, CancellationToken ct = default)
    {
        var path = $"/wp-json/wp/v2/{Uri.EscapeDataString(client.Options.RestBase)}/{remoteId}?context=edit";
        var res = await client.GetAsync(path, ct);

        if (res.Status == HttpStatusCode.NotFound) return null;
        if (!res.Ok || res.Body is null)
            throw new WordPressApiException(
                $"Could not read post {remoteId}: HTTP {(int)res.Status} {res.ErrorCode} {res.ErrorMessage}".TrimEnd(),
                res.Status, res.ErrorCode);

        return new RemotePostInfo(
            remoteId,
            res.Body["status"]?.ToString() ?? "",
            res.Body["link"]?.ToString(),
            res.Body["slug"]?.ToString());
    }

    public async Task<RemoteMedia> UploadMediaAsync(MediaUpload upload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        var path = "/wp-json/wp/v2/media";
        var res = await client.PostBytesAsync(path, upload.Bytes, upload.MimeType, upload.FileName, ct);
        if (!res.Ok || res.Body is null)
            throw new WordPressApiException(
                $"Image upload failed: HTTP {(int)res.Status} {res.ErrorCode} {res.ErrorMessage}".TrimEnd(),
                res.Status, res.ErrorCode);

        var id = res.Body["id"]?.GetValue<long>() ?? throw new WordPressApiException("Media response had no id.", res.Status, null);

        // Alt text is set in a second call; WordPress does not accept it on upload.
        var alt = await client.PostJsonAsync($"/wp-json/wp/v2/media/{id}", new Dictionary<string, object?> { ["alt_text"] = upload.AltText }, ct);
        if (!alt.Ok)
            throw new WordPressApiException(
                $"Image uploaded (id {id}) but alt text could not be set: HTTP {(int)alt.Status} {alt.ErrorCode}".TrimEnd(),
                alt.Status, alt.ErrorCode);

        return new RemoteMedia(id, res.Body["source_url"]?.ToString());
    }

    private Dictionary<string, object?> BuildPayload(DraftRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = request.Title,
            ["content"] = request.ContentHtml,
        };
        if (request.Excerpt is not null) payload["excerpt"] = request.Excerpt;
        if (request.Slug is not null) payload["slug"] = request.Slug;
        if (request.FeaturedMediaId is { } media) payload["featured_media"] = media;

        if (writeYoastMeta)
        {
            var meta = new Dictionary<string, object?>();
            if (request.SeoTitle is not null) meta[YoastTitleKey] = request.SeoTitle;
            if (request.MetaDescription is not null) meta[YoastDescriptionKey] = request.MetaDescription;
            if (meta.Count > 0) payload["meta"] = meta;
        }
        return payload;
    }

    private string EditUrl(long id) =>
        $"{client.Options.RestOrigin.AbsoluteUri.TrimEnd('/')}/wp-admin/post.php?post={id}&action=edit";
}
