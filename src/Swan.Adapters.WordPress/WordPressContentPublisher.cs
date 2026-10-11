using Swan.Adapters.Abstractions;

namespace Swan.Adapters.WordPress;

/// <summary>
/// Creates WordPress drafts. Safety rules for this slice:
/// <list type="bullet">
/// <item>Status is always "draft". Nothing here can publish.</item>
/// <item>Updating an existing post is not implemented, so a draft write can never overwrite a live post.</item>
/// </list>
/// </summary>
public sealed class WordPressContentPublisher(WordPressRestClient client) : IContentPublisher
{
    public async Task<RemoteDraftResult> CreateDraftAsync(DraftRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentNullException.ThrowIfNull(request.ContentHtml);

        var payload = new Dictionary<string, object?>
        {
            ["title"] = request.Title,
            ["content"] = request.ContentHtml,
            ["status"] = "draft",
        };
        if (request.Excerpt is not null) payload["excerpt"] = request.Excerpt;
        if (request.Slug is not null) payload["slug"] = request.Slug;

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

        var link = res.Body["link"]?.ToString();
        var editUrl = $"{client.Options.RestOrigin.AbsoluteUri.TrimEnd('/')}/wp-admin/post.php?post={id}&action=edit";
        return new RemoteDraftResult(id, status, link, editUrl);
    }
}
