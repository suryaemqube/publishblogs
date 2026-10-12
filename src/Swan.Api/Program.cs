using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Swan.Adapters.Abstractions;
using Swan.Adapters.WordPress;
using Swan.Api.Cli;
using Swan.Api.Documents;
using Swan.Api.Articles;
using Swan.Infrastructure.Content;
using Swan.Infrastructure.Content;
using Swan.Infrastructure.Storage;
using Swan.Api.Sites;
using Swan.Domain.Identity;
using Swan.Infrastructure.Auth;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Security;

// Offline admin command: dotnet run --project src/Swan.Api -- create-user --email ... --name ... --role Administrator
if (args.Length > 0 && args[0] == "create-user")
{
    return await CreateUserCommand.RunAsync(args[1..]);
}

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration -------------------------------------------------------------------------
var dbPath = builder.Configuration["Database:ConnectionString"] ?? ApiDatabase.ConnectionString();
var authSection = builder.Configuration.GetSection("Auth");
var signingKeyFile = authSection["SigningKeyFile"]
                     ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".swan-secrets", "jwt-signing-key.txt");
var authOptions = new AuthOptions
{
    Issuer = authSection["Issuer"] ?? "swan-api",
    Audience = authSection["Audience"] ?? "swan-web",
    AccessTokenMinutes = int.TryParse(authSection["AccessTokenMinutes"], out var m) ? m : 15,
    RefreshTokenDays = int.TryParse(authSection["RefreshTokenDays"], out var d) ? d : 14,
    SigningKey = SigningKeyStore.LoadOrCreate(signingKeyFile),
};

var siteConfigs = builder.Configuration.GetSection("Sites").Get<List<SiteConfig>>() ?? [];

// ---- Services ------------------------------------------------------------------------------
builder.Services.AddSingleton(new SiteRegistry(siteConfigs));
builder.Services.AddSingleton(authOptions);
builder.Services.AddDbContext<SwanDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton(new LocalFileStore(LocalFileStore.DefaultRoot()));
builder.Services.AddScoped<DocumentIngestion>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<DraftSync>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep "perm" and "role" claim names as issued
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(authOptions.SigningKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });

// One policy per permission key. Checked on the server; the UI never decides.
var authz = builder.Services.AddAuthorizationBuilder();
foreach (var key in PermissionKeys.All)
{
    authz.AddPolicy(key, p => p.RequireAuthenticatedUser().RequireClaim("perm", key));
}

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Auth endpoints: 10 attempts per minute per client address (PLAN-v2 §11.3).
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Dev convenience: create or update the schema on startup. Replace with reviewed migrations in production.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SwanDbContext>().Database.MigrateAsync();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// ---- Auth ----------------------------------------------------------------------------------
var auth = app.MapGroup("/api/auth").RequireRateLimiting("auth");

auth.MapPost("/login", async (LoginBody body, AuthService svc, HttpContext http, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrEmpty(body.Password))
        return Results.BadRequest(new { error = "Email and password are required." });

    var result = await svc.LoginAsync(body.Email, body.Password, http.Connection.RemoteIpAddress?.ToString(), ct);
    return result.Outcome switch
    {
        LoginOutcome.Success => Results.Ok(TokenResponse(result.Tokens!)),
        LoginOutcome.Locked => Results.Json(new { error = "Account temporarily locked after repeated failures.", lockedUntilUtc = result.LockedUntilUtc },
                                            statusCode: StatusCodes.Status423Locked),
        // Same response for unknown email, wrong password and inactive account: no account enumeration.
        _ => Results.Json(new { error = "Email or password is incorrect." }, statusCode: StatusCodes.Status401Unauthorized),
    };
});

auth.MapPost("/refresh", async (RefreshBody body, AuthService svc, HttpContext http, CancellationToken ct) =>
{
    var pair = await svc.RefreshAsync(body.RefreshToken ?? "", http.Connection.RemoteIpAddress?.ToString(), ct);
    return pair is null
        ? Results.Json(new { error = "Session expired. Sign in again." }, statusCode: StatusCodes.Status401Unauthorized)
        : Results.Ok(TokenResponse(pair));
});

auth.MapPost("/logout", async (RefreshBody body, AuthService svc, HttpContext http, CancellationToken ct) =>
{
    if (!string.IsNullOrEmpty(body.RefreshToken))
        await svc.LogoutAsync(body.RefreshToken, http.Connection.RemoteIpAddress?.ToString(), ct);
    return Results.NoContent();
});

app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, AuthService svc, CancellationToken ct) =>
{
    var id = principal.FindFirstValue("sub");
    if (!Guid.TryParse(id, out var userId)) return Results.Unauthorized();
    var me = await svc.GetCurrentUserAsync(userId, ct);
    return me is null ? Results.Unauthorized() : Results.Ok(me);
}).RequireAuthorization();

// ---- Sites ---------------------------------------------------------------------------------
var sites = app.MapGroup("/api/sites").RequireAuthorization();

sites.MapGet("/", (SiteRegistry registry) => Results.Ok(registry.List()))
    .RequireAuthorization(PermissionKeys.SiteRead);

// Read-only. Makes outbound calls to the client's WordPress REST API; never writes.
sites.MapGet("/{id}/connection-test", async (string id, SiteRegistry registry, CancellationToken ct) =>
{
    var site = registry.Find(id);
    if (site is null) return Results.NotFound(new { error = $"Unknown site '{id}'." });
    if (!TryCreateClient(registry, site, out var client, out var error)) return error!;
    using (client)
    {
        var report = await new WordPressConnectionTest(client!).RunAsync(ct);
        return Results.Ok(report);
    }
}).RequireAuthorization(PermissionKeys.SiteRead);

// Creates a WordPress draft. Status is forced to 'draft' by the adapter; never publishes.
sites.MapPost("/{id}/drafts", async (string id, CreateDraftRequest body, SiteRegistry registry,
                                     SwanDbContext db, ClaimsPrincipal principal, HttpContext http, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.Title)) return Results.BadRequest(new { error = "Title is required." });
    if (body.Title.Length > 300) return Results.BadRequest(new { error = "Title must be 300 characters or fewer." });
    if (body.ContentHtml is null) return Results.BadRequest(new { error = "ContentHtml is required." });

    var site = registry.Find(id);
    if (site is null) return Results.NotFound(new { error = $"Unknown site '{id}'." });
    if (!TryCreateClient(registry, site, out var client, out var error)) return error!;

    using (client)
    {
        try
        {
            var result = await new WordPressContentPublisher(client!)
                .CreateDraftAsync(new DraftRequest(body.Title, body.ContentHtml, body.Excerpt, body.Slug), ct);

            db.AuditLogs.Add(new AuditLog
            {
                UserId = Guid.TryParse(principal.FindFirstValue("sub"), out var uid) ? uid : null,
                Action = "publishing.draft.created",
                EntityType = "WordPressPost",
                EntityId = $"{site.Id}:{result.RemoteId}",
                Details = $"status={result.Status}",
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
                CreatedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);

            return Results.Created(result.EditUrl, result);
        }
        catch (WordPressApiException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "Site rejected the draft");
        }
    }
}).RequireAuthorization(PermissionKeys.PublishingDraft);

app.MapDocumentEndpoints();
app.MapArticleEndpoints();

app.Run();
return 0;

// Turns a configuration problem (missing/empty credentials file, missing key) into a readable 503.
// Messages name the file and key, never the value.
static bool TryCreateClient(SiteRegistry sites, SiteConfig site, out WordPressRestClient? client, out IResult? error)
{
    try
    {
        client = sites.CreateWordPressClient(site);
        error = null;
        return true;
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or UriFormatException)
    {
        client = null;
        error = Results.Problem(
            $"Site '{site.Id}' is not configured: {ex.Message}",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Site configuration incomplete");
        return false;
    }
}

static object TokenResponse(TokenPair p) => new
{
    accessToken = p.AccessToken,
    accessExpiresUtc = p.AccessExpiresUtc,
    refreshToken = p.RefreshToken,
    refreshExpiresUtc = p.RefreshExpiresUtc,
};

public sealed record CreateDraftRequest(string Title, string ContentHtml, string? Excerpt, string? Slug);
public sealed record LoginBody(string Email, string Password);
public sealed record RefreshBody(string? RefreshToken);

