// Swan.Probe - standalone WordPress connection probe (PLAN-v2 §15, "do this first").
//
// Usage:
//   dotnet run --project tools/Swan.Probe                    # read-only connection test
//   dotnet run --project tools/Swan.Probe -- --create-draft  # also writes ONE draft (status=draft)
//   dotnet run --project tools/Swan.Probe -- --creds <path>  # default: %USERPROFILE%\.swan-secrets\creds.txt
//
// The password is never printed. Nothing here can publish.

using Swan.Adapters.Abstractions;
using Swan.Adapters.WordPress;

var credsPath = CredentialFile.DefaultPath();
var createDraft = false;
string? userAgent = null;
string? inspectPostId = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--creds" && i + 1 < args.Length) credsPath = args[++i];
    else if (args[i] == "--create-draft") createDraft = true;
    else if (args[i] == "--user-agent" && i + 1 < args.Length) userAgent = args[++i];
    else if (args[i] == "--inspect-post" && i + 1 < args.Length) inspectPostId = args[++i];
}

// Read-only: prints a post's status and the NAMES of its meta keys (never values).
if (inspectPostId is not null)
{
    var s = CredentialFile.Load(credsPath);
    using var c = new WordPressRestClient(new WordPressSiteOptions(
        new Uri(CredentialFile.Require(s, "RestOrigin"), UriKind.Absolute),
        CredentialFile.Require(s, "PostTypeKey"),
        CredentialFile.Require(s, "RestBase"),
        CredentialFile.Require(s, "Username"),
        CredentialFile.Require(s, "ApplicationPassword"),
        userAgent));
    var res = await c.GetAsync($"/wp-json/wp/v2/{CredentialFile.Require(s, "RestBase")}/{inspectPostId}?context=edit");
    Console.WriteLine($"HTTP {(int)res.Status}");
    if (res.Body is null) return 1;
    Console.WriteLine($"status: {res.Body["status"]}");
    Console.WriteLine($"slug: {res.Body["slug"]}");
    Console.WriteLine($"link: {res.Body["link"]}");
    Console.WriteLine("meta keys:");
    if (res.Body["meta"] is System.Text.Json.Nodes.JsonObject meta)
        foreach (var k in meta.Select(kv => kv.Key)) Console.WriteLine($"  {k}");
    else
        Console.WriteLine("  (meta not exposed in REST)");
    if (res.Body["yoast_head_json"] is not null) Console.WriteLine("yoast_head_json: present");
    return 0;
}

WordPressSiteOptions options;
try
{
    var settings = CredentialFile.Load(credsPath);
    options = new WordPressSiteOptions(
        new Uri(CredentialFile.Require(settings, "RestOrigin"), UriKind.Absolute),
        CredentialFile.Require(settings, "PostTypeKey"),
        CredentialFile.Require(settings, "RestBase"),
        CredentialFile.Require(settings, "Username"),
        CredentialFile.Require(settings, "ApplicationPassword"),
        userAgent);
}
catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or UriFormatException)
{
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    Console.Error.WriteLine("Expected key=value lines: RestOrigin, PostTypeKey, RestBase, Username, ApplicationPassword");
    return 2;
}

using var client = new WordPressRestClient(options);
var report = await new WordPressConnectionTest(client).RunAsync();

Console.WriteLine($"Site: {options.RestOrigin}  post type: {options.PostTypeKey}  rest_base: {options.RestBase}");
Console.WriteLine();
foreach (var c in report.Checks)
{
    Console.WriteLine($"  [{(c.Passed ? "PASS" : "FAIL")}] {c.Name,-34} {c.Detail}");
}
Console.WriteLine();
Console.WriteLine(report.Passed ? "Connection test: PASSED" : "Connection test: FAILED");

if (!report.Passed) return 1;

if (createDraft)
{
    var publisher = new WordPressContentPublisher(client);
    var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");
    var result = await publisher.CreateDraftAsync(new DraftRequest(
        Title: $"[Swan test - delete me] {stamp}",
        ContentHtml: "<p>Test draft created by Swan.Probe to verify draft writes. Safe to delete.</p>"));

    Console.WriteLine();
    Console.WriteLine($"Draft created: id={result.RemoteId} status={result.Status}");
    Console.WriteLine($"Edit in WP admin: {result.EditUrl}");
}

return 0;
