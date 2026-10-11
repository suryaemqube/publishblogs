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
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--creds" && i + 1 < args.Length) credsPath = args[++i];
    else if (args[i] == "--create-draft") createDraft = true;
    else if (args[i] == "--user-agent" && i + 1 < args.Length) userAgent = args[++i];
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
