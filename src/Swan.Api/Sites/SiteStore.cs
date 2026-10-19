using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Swan.Domain.Sites;
using Swan.Infrastructure.Persistence;

namespace Swan.Api.Sites;

/// <summary>
/// Reads and writes client sites. The only code that encrypts Application Passwords. After every write the
/// <see cref="SiteRegistry"/> snapshot is refreshed, so a new site works without restarting the API.
/// </summary>
public sealed class SiteStore(SwanDbContext db, SiteRegistry registry, IDataProtectionProvider protection)
{
    public const string SecretPurpose = "Swan.SiteSecrets";

    private readonly IDataProtector _protector = protection.CreateProtector(SecretPurpose);

    public string Protect(string password) => _protector.Protect(password);

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var rows = await db.ClientSites.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        var secrets = await db.SiteSecrets.AsNoTracking()
            .ToDictionaryAsync(s => s.SiteId, s => s.ProtectedApplicationPassword, ct);
        registry.Replace(rows.Select(r => ToConfig(r, secrets.GetValueOrDefault(r.Id) ?? "")));
    }

    /// <summary>Adds a site and its encrypted password. The caller has validated the fields.</summary>
    public async Task CreateAsync(ClientSite site, string plainPassword, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        site.CreatedUtc = now;
        site.UpdatedUtc = now;
        db.ClientSites.Add(site);
        db.SiteSecrets.Add(new SiteSecret { SiteId = site.Id, ProtectedApplicationPassword = Protect(plainPassword), UpdatedUtc = now });
        await db.SaveChangesAsync(ct);
        await RefreshAsync(ct);
    }

    /// <summary>
    /// Saves changes to a tracked site. A new password replaces the stored one; null keeps it. Any change to how Swan
    /// reaches the site resets the connection status, so drafts stay blocked until a new test passes.
    /// </summary>
    public async Task SaveAsync(ClientSite site, string? newPlainPassword, bool connectionChanged, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        site.UpdatedUtc = now;
        if (connectionChanged || newPlainPassword is not null)
        {
            site.ConnectionStatus = "Untested";
            site.LastTestedUtc = null;
            site.LastTestSummary = null;
        }
        if (newPlainPassword is not null)
        {
            var secret = await db.SiteSecrets.FirstOrDefaultAsync(s => s.SiteId == site.Id, ct);
            if (secret is null)
                db.SiteSecrets.Add(new SiteSecret { SiteId = site.Id, ProtectedApplicationPassword = Protect(newPlainPassword), UpdatedUtc = now });
            else
            {
                secret.ProtectedApplicationPassword = Protect(newPlainPassword);
                secret.UpdatedUtc = now;
            }
        }
        await db.SaveChangesAsync(ct);
        await RefreshAsync(ct);
    }

    public async Task SetStatusAsync(string siteId, string status, string summary, CancellationToken ct = default)
    {
        var site = await db.ClientSites.FirstOrDefaultAsync(s => s.Id == siteId, ct);
        if (site is null) return;
        site.ConnectionStatus = status;
        site.LastTestedUtc = DateTime.UtcNow;
        site.LastTestSummary = summary.Length > 1000 ? summary[..1000] : summary;
        await db.SaveChangesAsync(ct);
        await RefreshAsync(ct);
    }

    public static SiteConfig ToConfig(ClientSite r, string protectedPassword) => new()
    {
        Id = r.Id,
        Name = r.Name,
        RestOrigin = r.RestOrigin,
        PostTypeKey = r.PostTypeKey,
        RestBase = r.RestBase,
        Username = r.Username,
        ProtectedPassword = protectedPassword,
        Taxonomies = JsonSerializer.Deserialize<List<string>>(r.TaxonomiesJson) ?? [],
        WriteYoastMeta = r.WriteYoastMeta,
        UserAgent = r.UserAgent,
        IsEnabled = r.IsEnabled,
        ConnectionStatus = r.ConnectionStatus,
        LastTestedUtc = r.LastTestedUtc,
        LastTestSummary = r.LastTestSummary,
    };

    public static string TaxonomiesToJson(IEnumerable<string> names) => JsonSerializer.Serialize(names.Distinct().ToList());
}
