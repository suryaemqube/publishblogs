using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Swan.Adapters.Abstractions;
using Swan.Adapters.WordPress;
using Swan.Domain.Identity;
using Swan.Domain.Sites;
using Swan.Infrastructure.Persistence;

namespace Swan.Api.Sites;

public sealed record SiteBody(
    string? Name,
    string? RestOrigin,
    string? Username,
    string? ApplicationPassword,
    string? PostTypeKey,
    string? RestBase,
    List<string>? Taxonomies,
    bool WriteYoastMeta,
    bool IsEnabled = true,
    string? UserAgent = null);

public sealed record DiscoverBody(string? RestOrigin, string? Username, string? ApplicationPassword);

public static class SiteEndpoints
{
    private static readonly Regex Slug = new("^[a-z0-9_-]{1,60}$", RegexOptions.Compiled);
    private static readonly string[] YoastKeys = WordPressContentPublisher.YoastMetaKeys;

    public static void MapSiteEndpoints(this WebApplication app)
    {
        var sites = app.MapGroup("/api/sites").RequireAuthorization();

        sites.MapGet("/", (SiteRegistry registry) => Results.Ok(registry.List()))
            .RequireAuthorization(PermissionKeys.SiteRead);

        sites.MapGet("/{id}", async (string id, SwanDbContext db, CancellationToken ct) =>
        {
            var row = await db.ClientSites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (row is null) return Results.NotFound(new { error = $"Unknown site '{id}'." });
            var hasPassword = await db.SiteSecrets.AnyAsync(s => s.SiteId == id, ct);
            return Results.Ok(Detail(SiteStore.ToConfig(row, ""), hasPassword));
        }).RequireAuthorization(PermissionKeys.SiteManage);

        sites.MapPost("/", async (SiteBody body, SiteStore store, SiteRegistry registry, SwanDbContext db,
                                  ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
        {
            var v = Validate(body, creating: true);
            if (v.Error is not null) return Results.BadRequest(new { error = v.Error });

            var id = Slugify(v.Name!);
            if (string.IsNullOrEmpty(id)) return Results.BadRequest(new { error = "The name must contain letters or numbers." });
            if (await db.ClientSites.AnyAsync(s => s.Id == id, ct))
                return Results.Json(new { error = $"A site with the id '{id}' already exists. Use a different name." },
                                    statusCode: StatusCodes.Status409Conflict);

            var site = new ClientSite
            {
                Id = id,
                Name = v.Name!,
                RestOrigin = v.Origin!,
                PostTypeKey = v.PostTypeKey!,
                RestBase = v.RestBase!,
                Username = v.Username!,
                TaxonomiesJson = SiteStore.TaxonomiesToJson(v.Taxonomies!),
                WriteYoastMeta = body.WriteYoastMeta,
                UserAgent = string.IsNullOrWhiteSpace(body.UserAgent) ? null : body.UserAgent.Trim(),
                IsEnabled = body.IsEnabled,
            };
            await store.CreateAsync(site, v.Password!, ct);
            await Audit(db, principal, http, "site.created", id, $"name={site.Name} origin={site.RestOrigin}", ct);

            // Save first, then test: the result is recorded on the site either way.
            var report = await TestAndRecordAsync(id, registry, store, ct);
            return Results.Created($"/api/sites/{id}", new { site = Summary(registry.Find(id)!), report });
        }).RequireAuthorization(PermissionKeys.SiteManage);

        sites.MapPut("/{id}", async (string id, SiteBody body, SiteStore store, SiteRegistry registry, SwanDbContext db,
                                     ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
        {
            var site = await db.ClientSites.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (site is null) return Results.NotFound(new { error = $"Unknown site '{id}'." });

            var v = Validate(body, creating: false);
            if (v.Error is not null) return Results.BadRequest(new { error = v.Error });

            var before = $"{site.RestOrigin}|{site.PostTypeKey}|{site.RestBase}|{site.Username}|{site.TaxonomiesJson}|{site.WriteYoastMeta}|{site.UserAgent}";
            site.Name = v.Name!;
            site.RestOrigin = v.Origin!;
            site.PostTypeKey = v.PostTypeKey!;
            site.RestBase = v.RestBase!;
            site.Username = v.Username!;
            site.TaxonomiesJson = SiteStore.TaxonomiesToJson(v.Taxonomies!);
            site.WriteYoastMeta = body.WriteYoastMeta;
            site.UserAgent = string.IsNullOrWhiteSpace(body.UserAgent) ? null : body.UserAgent.Trim();
            site.IsEnabled = body.IsEnabled;

            var after = $"{site.RestOrigin}|{site.PostTypeKey}|{site.RestBase}|{site.Username}|{site.TaxonomiesJson}|{site.WriteYoastMeta}|{site.UserAgent}";
            var newPassword = string.IsNullOrWhiteSpace(body.ApplicationPassword) ? null : body.ApplicationPassword;
            await store.SaveAsync(site, newPassword, connectionChanged: before != after, ct);
            await Audit(db, principal, http, "site.updated", id,
                        $"connectionChanged={before != after} passwordChanged={newPassword is not null}", ct);
            return Results.Ok(new { site = Summary(registry.Find(id)!) });
        }).RequireAuthorization(PermissionKeys.SiteManage);

        // Enable or disable. A disabled site cannot receive drafts. Nothing is deleted.
        sites.MapPost("/{id}/enabled", async (string id, EnabledBody body, SiteStore store, SiteRegistry registry,
                                              SwanDbContext db, ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
        {
            var site = await db.ClientSites.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (site is null) return Results.NotFound(new { error = $"Unknown site '{id}'." });
            site.IsEnabled = body.Enabled;
            await store.SaveAsync(site, null, connectionChanged: false, ct);
            await Audit(db, principal, http, body.Enabled ? "site.enabled" : "site.disabled", id, "", ct);
            return Results.Ok(new { site = Summary(registry.Find(id)!) });
        }).RequireAuthorization(PermissionKeys.SiteManage);

        // Lists the post types and taxonomies WordPress exposes over REST, so the form can offer them.
        // Writes nothing. Uses the values typed into the form.
        sites.MapPost("/discover", async (DiscoverBody body, CancellationToken ct) =>
        {
            var origin = NormalizeOrigin(body.RestOrigin);
            if (origin is null) return Results.BadRequest(new { error = "Enter the WordPress address, for example https://example.com." });
            if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.ApplicationPassword))
                return Results.BadRequest(new { error = "Enter the username and Application Password." });
            return await DiscoverAsync(origin, body.Username.Trim(), body.ApplicationPassword, ct);
        }).RequireAuthorization(PermissionKeys.SiteManage);

        // Same as above, using the password already stored for the site (for the edit form).
        sites.MapPost("/{id}/discover", async (string id, SiteRegistry registry, CancellationToken ct) =>
        {
            var site = registry.Find(id);
            if (site is null) return Results.NotFound(new { error = $"Unknown site '{id}'." });
            using var client = SafeClient(registry, site, out var error);
            if (client is null) return error!;
            return await DiscoverAsync(site.RestOrigin, site.Username, null, ct, client);
        }).RequireAuthorization(PermissionKeys.SiteManage);

        // Read-only checks against the site. Records the result, which decides whether drafts are allowed.
        sites.MapGet("/{id}/connection-test", async (string id, SiteStore store, SiteRegistry registry, CancellationToken ct) =>
        {
            if (registry.Find(id) is null) return Results.NotFound(new { error = $"Unknown site '{id}'." });
            var report = await TestAndRecordAsync(id, registry, store, ct);
            return report is null
                ? Results.Problem("The site's stored password could not be read. Enter it again on the edit form.",
                                  statusCode: StatusCodes.Status503ServiceUnavailable, title: "Site configuration incomplete")
                : Results.Ok(report);
        }).RequireAuthorization(PermissionKeys.SiteRead);

        // Creates ONE draft. Refused unless the site is enabled and its last connection test passed.
        sites.MapPost("/{id}/drafts", async (string id, CreateDraftRequest body, SiteRegistry registry,
                                             SwanDbContext db, ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
        {
            if (!principal.HasClaim("perm", PermissionKeys.PublishingDraft))
                return Results.Json(new { error = "Your role cannot write drafts to sites." }, statusCode: StatusCodes.Status403Forbidden);
            if (string.IsNullOrWhiteSpace(body.Title)) return Results.BadRequest(new { error = "Title is required." });
            if (body.Title.Length > 300) return Results.BadRequest(new { error = "Title must be 300 characters or fewer." });
            if (body.ContentHtml is null) return Results.BadRequest(new { error = "ContentHtml is required." });

            var site = registry.Find(id);
            if (site is null) return Results.NotFound(new { error = $"Unknown site '{id}'." });
            if (!site.IsEnabled)
                return Results.Json(new { error = "This site is disabled. Enable it on the Sites screen first.", refused = "site_disabled" },
                                    statusCode: StatusCodes.Status409Conflict);
            if (site.ConnectionStatus != "Ready")
                return Results.Json(new { error = "This site has not passed its connection test. Run Test connection on the Sites screen and fix any failed checks.", refused = "site_not_ready" },
                                    statusCode: StatusCodes.Status409Conflict);

            using var client = SafeClient(registry, site, out var error);
            if (client is null) return error!;
            try
            {
                var result = await new WordPressContentPublisher(client)
                    .CreateDraftAsync(new DraftRequest(body.Title, body.ContentHtml, body.Excerpt, body.Slug), ct);

                await Audit(db, principal, http, "publishing.draft.created", $"{site.Id}:{result.RemoteId}", $"status={result.Status}", ct);
                return Results.Created(result.EditUrl, result);
            }
            catch (WordPressApiException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "Site rejected the draft");
            }
        }).RequireAuthorization(PermissionKeys.PublishingDraft);
    }

    public sealed record EnabledBody(bool Enabled);

    // ---- helpers ------------------------------------------------------------------------------------------

    private sealed record Validated(string? Error, string? Name, string? Origin, string? Username, string? Password,
                                    string? PostTypeKey, string? RestBase, List<string>? Taxonomies);

    private static Validated Validate(SiteBody b, bool creating)
    {
        var name = b.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 200) return Fail("Enter a name for the site (up to 200 characters).");
        var origin = NormalizeOrigin(b.RestOrigin);
        if (origin is null) return Fail("Enter the WordPress address, for example https://example.com.");
        var username = b.Username?.Trim() ?? "";
        if (username.Length == 0 || username.Length > 200) return Fail("Enter the WordPress username.");
        if (creating && string.IsNullOrWhiteSpace(b.ApplicationPassword)) return Fail("Enter the Application Password.");
        var postType = b.PostTypeKey?.Trim() ?? "";
        if (!Slug.IsMatch(postType)) return Fail("Choose the post type (lowercase letters, numbers, - or _).");
        var restBase = b.RestBase?.Trim() ?? "";
        if (!Slug.IsMatch(restBase)) return Fail("Choose the REST base for the post type (lowercase letters, numbers, - or _).");
        var taxonomies = (b.Taxonomies ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().ToList();
        if (taxonomies.Any(t => !Slug.IsMatch(t))) return Fail("Taxonomy names may only contain lowercase letters, numbers, - or _.");
        return new Validated(null, name, origin, username, b.ApplicationPassword, postType, restBase, taxonomies);
    }

    private static Validated Fail(string message) => new(message, null, null, null, null, null, null, null);

    /// <summary>scheme://host[:port] only. Paths are dropped, because Swan always calls the REST API from the origin.</summary>
    public static string? NormalizeOrigin(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var text = input.Trim();
        if (!text.Contains("://")) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;
        return uri.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>Lowercase slug from the name. Stable once created, because articles and drafts refer to it.</summary>
    public static string Slugify(string name)
    {
        var chars = name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray();
        return Regex.Replace(new string(chars), "-+", "-").Trim('-');
    }

    private static SiteSummary Summary(SiteConfig s) => SiteRegistry.Summarize(s);

    private static object Detail(SiteConfig s, bool hasPassword) => new
    {
        id = s.Id,
        name = s.Name,
        restOrigin = s.RestOrigin,
        postTypeKey = s.PostTypeKey,
        restBase = s.RestBase,
        username = s.Username,
        taxonomies = s.Taxonomies,
        writeYoastMeta = s.WriteYoastMeta,
        userAgent = s.UserAgent,
        isEnabled = s.IsEnabled,
        connectionStatus = s.ConnectionStatus,
        lastTestedUtc = s.LastTestedUtc,
        lastTestSummary = s.LastTestSummary,
        hasPassword,
    };

    private static WordPressRestClient? SafeClient(SiteRegistry registry, SiteConfig site, out IResult? error)
    {
        try
        {
            error = null;
            return registry.CreateWordPressClient(site);
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Security.Cryptography.CryptographicException or UriFormatException)
        {
            error = Results.Problem($"Site '{site.Id}' is not configured: {ex.Message}",
                                    statusCode: StatusCodes.Status503ServiceUnavailable, title: "Site configuration incomplete");
            return null;
        }
    }

    /// <summary>Runs the connection checks and records the outcome on the site. Returns null if the password cannot be read.</summary>
    private static async Task<ConnectionTestReport?> TestAndRecordAsync(string id, SiteRegistry registry, SiteStore store, CancellationToken ct)
    {
        var site = registry.Find(id);
        if (site is null) return null;
        using var client = SafeClient(registry, site, out _);
        if (client is null) return null;

        ConnectionTestReport report;
        try
        {
            report = await new WordPressConnectionTest(client).RunAsync(ct,
                site.WriteYoastMeta ? YoastKeys : null,
                site.Taxonomies.Count > 0 ? site.Taxonomies : null);
        }
        // DNS failure, connection refused, TLS failure, timeout: report it as a failed check rather than a 500,
        // so adding a mistyped site address is a readable result and the test is still recorded.
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            report = new ConnectionTestReport([new ConnectionCheck("Reachability", false,
                $"Could not reach {site.RestOrigin}: {ex.Message}")]);
        }

        var failed = report.Checks.Where(c => !c.Passed).ToList();
        var summary = report.Passed
            ? "All checks passed."
            : string.Join("; ", failed.Select(c => $"{c.Name}: {c.Detail}"));
        await store.SetStatusAsync(id, report.Passed ? "Ready" : "NotReady", summary, ct);
        return report;
    }

    private static async Task<IResult> DiscoverAsync(string origin, string username, string? password, CancellationToken ct,
                                                     WordPressRestClient? existing = null)
    {
        using var client = existing ?? new WordPressRestClient(new WordPressSiteOptions(
            new Uri(origin, UriKind.Absolute), "post", "posts", username, password ?? "", null));

        WpResponse types;
        WpResponse taxonomies;
        try
        {
            types = await client.GetAsync("/wp-json/wp/v2/types", ct);
            taxonomies = await client.GetAsync("/wp-json/wp/v2/taxonomies", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Results.Json(new { error = $"Could not reach {origin}: {ex.Message}" },
                                statusCode: StatusCodes.Status502BadGateway);
        }

        if (!types.Ok)
            return Results.Json(new { error = $"WordPress did not list its post types: HTTP {(int)types.Status} {types.ErrorMessage}".TrimEnd() },
                                statusCode: StatusCodes.Status502BadGateway);

        return Results.Ok(new
        {
            types = Entries(types.Body),
            taxonomies = Entries(taxonomies.Ok ? taxonomies.Body : null),
        });
    }

    /// <summary>WordPress returns an object keyed by slug, each with rest_base and name.</summary>
    private static List<object> Entries(JsonNode? body)
    {
        var entries = new List<(string Slug, string RestBase, string Name)>();
        if (body is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                var rest = kv.Value?["rest_base"]?.ToString();
                if (string.IsNullOrEmpty(rest)) continue;
                entries.Add((kv.Key, rest, kv.Value?["name"]?.ToString() ?? kv.Key));
            }
        }
        return entries
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => (object)new { slug = e.Slug, restBase = e.RestBase, name = e.Name })
            .ToList();
    }

    private static async Task Audit(SwanDbContext db, ClaimsPrincipal principal, HttpContext http, string action, string entityId,
                                    string details, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = Guid.TryParse(principal.FindFirstValue("sub"), out var uid) ? uid : null,
            Action = action,
            EntityType = "ClientSite",
            EntityId = entityId,
            Details = details,
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
