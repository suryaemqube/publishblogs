using System.Net;
using Swan.Adapters.Abstractions;

namespace Swan.Adapters.WordPress;

/// <summary>
/// Read-only connection test (PLAN-v2 §8.4). It never writes to the site.
/// Every check asserts a positive 200, because a post type that is withheld from REST
/// returns 401, not 404, and a "not 404" check would pass on an unpublishable site.
/// </summary>
public sealed class WordPressConnectionTest(WordPressRestClient client)
{
    /// <summary>
    /// Runs the checks. <paramref name="requiredMetaKeys"/> are meta keys that must be exposed over REST for the post
    /// type (for example Yoast keys, when Swan writes them). Null or empty skips that check.
    /// </summary>
    /// <param name="requiredTaxonomies">
    /// Taxonomies Swan must write to (for example the category editors use). Each must be attached to the post type
    /// AND readable over REST. The post type's own list is not trusted on its own: a taxonomy can be used on the site
    /// while the type reports none.
    /// </param>
    public async Task<ConnectionTestReport> RunAsync(CancellationToken ct = default,
        IReadOnlyList<string>? requiredMetaKeys = null,
        IReadOnlyList<string>? requiredTaxonomies = null)
    {
        var checks = new List<ConnectionCheck>();
        var opts = client.Options;

        // 1. REST root responds and exposes wp/v2.
        var root = await client.GetAsync("/wp-json/", ct);
        var hasV2 = root.Body?["namespaces"] is System.Text.Json.Nodes.JsonArray ns
                    && ns.Any(n => n?.ToString() == "wp/v2");
        checks.Add(new ConnectionCheck("REST root",
            root.Ok && hasV2,
            root.Ok
                ? (hasV2 ? $"{root.Body?["name"]} (wp/v2 present)" : "responds, but wp/v2 namespace is missing")
                : Describe(root)));

        // 2. Credentials authenticate.
        var me = await client.GetAsync("/wp-json/wp/v2/users/me", ct);
        checks.Add(new ConnectionCheck("Authentication",
            me.Ok,
            me.Ok ? $"authenticated as '{me.Body?["name"]}'" : Describe(me)));

        // 3. The configured post type is REST-exposed.
        var type = await client.GetAsync($"/wp-json/wp/v2/types/{Uri.EscapeDataString(opts.PostTypeKey)}", ct);
        var typeTaxonomies = new List<string>();
        if (type.Ok)
        {
            var actualRestBase = type.Body?["rest_base"]?.ToString();
            var baseMatches = string.Equals(actualRestBase, opts.RestBase, StringComparison.Ordinal);
            checks.Add(new ConnectionCheck("Post type REST-exposed",
                baseMatches,
                baseMatches
                    ? $"'{opts.PostTypeKey}' exposed at rest_base '{actualRestBase}'"
                    : $"'{opts.PostTypeKey}' is exposed at rest_base '{actualRestBase}', configured as '{opts.RestBase}'"));

            if (type.Body?["taxonomies"] is System.Text.Json.Nodes.JsonArray tax)
                typeTaxonomies.AddRange(tax.Select(t => t!.ToString()));
        }
        else if (type.Status == HttpStatusCode.Unauthorized)
        {
            checks.Add(new ConnectionCheck("Post type REST-exposed", false,
                $"'{opts.PostTypeKey}' is registered but withheld from REST ({type.ErrorCode}). Needs show_in_rest => true on the client site."));
        }
        else
        {
            checks.Add(new ConnectionCheck("Post type REST-exposed", false,
                $"'{opts.PostTypeKey}' not found: {Describe(type)}"));
        }

        // 3b. Meta keys Swan writes must be registered for REST, or WordPress drops them without an error.
        if (requiredMetaKeys is { Count: > 0 } && type.Ok)
        {
            var restBase = Uri.EscapeDataString(opts.RestBase);
            var schema = await client.OptionsAsync($"/wp-json/wp/v2/{restBase}", ct);
            if (!schema.Ok)
            {
                checks.Add(new ConnectionCheck("Meta keys exposed over REST", false,
                    $"Could not read the '{opts.RestBase}' schema: {Describe(schema)}"));
            }
            else
            {
                var exposed = schema.Body?["schema"]?["properties"]?["meta"]?["properties"] is System.Text.Json.Nodes.JsonObject meta
                    ? meta.Select(p => p.Key).ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                var missing = requiredMetaKeys.Where(k => !exposed.Contains(k)).ToList();
                checks.Add(new ConnectionCheck("Meta keys exposed over REST",
                    missing.Count == 0,
                    missing.Count == 0
                        ? $"all {requiredMetaKeys.Count} keys are registered for '{opts.PostTypeKey}'"
                        : $"not registered for '{opts.PostTypeKey}': {string.Join(", ", missing)}. " +
                          "WordPress ignores these values silently. Install the REST meta snippet on the site (see the setup note)."));
            }
        }

        // 4. Each taxonomy attached to the post type, and each required taxonomy, is readable.
        //    rest_base is read from the site, never derived.
        if (requiredTaxonomies is { Count: > 0 } && type.Ok)
        {
            foreach (var required in requiredTaxonomies)
            {
                if (typeTaxonomies.Contains(required, StringComparer.Ordinal)) continue;
                checks.Add(new ConnectionCheck($"Taxonomy '{required}' attached to post type", false,
                    $"'{required}' is configured for Swan but '{opts.PostTypeKey}' does not list it. Editors' categories would not be written. " +
                    "Attach the taxonomy to the post type and set show_in_rest => true on it."));
            }
            typeTaxonomies.AddRange(requiredTaxonomies.Where(r => !typeTaxonomies.Contains(r, StringComparer.Ordinal)));
        }

        foreach (var name in typeTaxonomies.Distinct(StringComparer.Ordinal))
        {
            var t = await client.GetAsync($"/wp-json/wp/v2/taxonomies/{Uri.EscapeDataString(name)}", ct);
            checks.Add(new ConnectionCheck($"Taxonomy '{name}'",
                t.Ok,
                t.Ok
                    ? $"readable at rest_base '{t.Body?["rest_base"]}'"
                    : $"attached to '{opts.PostTypeKey}' but not readable: {Describe(t)}"));
        }

        return new ConnectionTestReport(checks);
    }

    private static string Describe(WpResponse r) =>
        $"HTTP {(int)r.Status}{(r.ErrorCode is null ? "" : $" {r.ErrorCode}")}{(r.ErrorMessage is null ? "" : $": {r.ErrorMessage}")}";
}
