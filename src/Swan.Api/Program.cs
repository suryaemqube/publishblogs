using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Swan.Adapters.Abstractions;
using Swan.Adapters.WordPress;
using Swan.Api.Cli;
using Swan.Api.Documents;
using Swan.Api.Articles;
using Swan.Infrastructure.Content;
using Swan.Infrastructure.Storage;
using Swan.Api.Sites;
using Swan.Api.Users;
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

// Application Passwords are encrypted with Data Protection. Set DataProtection:KeysPath to a persistent,
// backed-up location in production (for example /home/data/keys on Azure App Service). Losing this folder
// makes every stored WordPress Application Password unreadable, so it must be backed up with the database.
var protectionKeysDir = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".swan-data", "keys");

// Uploaded .docx originals and images. Set Storage:Root to persistent storage in production.
var storageRoot = builder.Configuration["Storage:Root"] ?? LocalFileStore.DefaultRoot();

// ---- Services ------------------------------------------------------------------------------
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(protectionKeysDir)).SetApplicationName("Swan");
builder.Services.AddSingleton<SiteRegistry>();
builder.Services.AddScoped<SiteStore>();
builder.Services.AddSingleton(authOptions);
builder.Services.AddDbContext<SwanDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserAdmin>();
builder.Services.AddSingleton(new LocalFileStore(storageRoot));
builder.Services.AddScoped<DocumentIngestion>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<DraftSync>();
builder.Services.AddScoped<FeaturedImageService>();

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
    p.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"])
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Dev convenience: create or update the schema on startup. Replace with reviewed migrations in production.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<SwanDbContext>();
    await db.Database.MigrateAsync();

    await services.GetRequiredService<SiteStore>().RefreshAsync();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Serve the built Angular app from wwwroot when it is present, so production is a single deployable on one origin.
// In development the client runs on the Angular dev server and this is a no-op.
app.UseDefaultFiles();
app.UseStaticFiles();

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

app.MapSiteEndpoints();

app.MapDocumentEndpoints();
app.MapArticleEndpoints();
app.MapUserEndpoints();
app.MapRoleEndpoints();

// Client-side routes (for example /sites/new) serve index.html, but an unknown /api path must still 404
// rather than returning the HTML shell. Only registered when the client has actually been built into wwwroot.
var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (File.Exists(Path.Combine(webRoot, "index.html")))
{
    app.MapFallback("/api/{**rest}", () => Results.NotFound());
    app.MapFallbackToFile("index.html");
}

app.Run();
return 0;

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


