using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Swan.Domain.Identity;
using Swan.Infrastructure.Auth;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Security;
using Xunit;

namespace Swan.Adapters.WordPress.Tests;

/// <summary>Local-account security behaviour. Uses a real SQLite database (in memory) with the seeded schema.</summary>
public sealed class AuthTests : IAsyncLifetime
{
    private const string Password = "correct horse battery";
    private SqliteConnection _conn = null!;
    private SwanDbContext _db = null!;
    private DateTimeOffset _now = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeClock _clock;

    public AuthTests() => _clock = new FakeClock(() => _now);

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        await _conn.OpenAsync();
        _db = new SwanDbContext(new DbContextOptionsBuilder<SwanDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        await new UserAdmin(_db, _clock).CreateUserAsync("writer@example.com", "Writer", Password, [RoleNames.Publisher]);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private AuthService Auth() => new(_db, Options(), _clock);

    private static AuthOptions Options() => new()
    {
        SigningKey = new byte[64],
        MaxFailedLogins = 5,
        LockoutMinutes = 15,
    };

    [Fact]
    public void Password_hash_is_argon2id_and_verifies()
    {
        var hash = PasswordHasher.Hash(Password);
        Assert.StartsWith("argon2id$", hash);
        Assert.True(PasswordHasher.Verify(Password, hash));
        Assert.False(PasswordHasher.Verify("wrong password", hash));
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ()
    {
        Assert.NotEqual(PasswordHasher.Hash(Password), PasswordHasher.Hash(Password));
    }

    [Fact]
    public async Task Correct_password_issues_tokens_and_permissions_come_from_role()
    {
        var result = await Auth().LoginAsync("Writer@Example.com", Password, "127.0.0.1");

        Assert.Equal(LoginOutcome.Success, result.Outcome);
        Assert.NotNull(result.Tokens);
        var me = await Auth().GetCurrentUserAsync((await _db.Users.SingleAsync()).Id);
        Assert.Contains(PermissionKeys.PublishingDraft, me!.Permissions);
        Assert.DoesNotContain(PermissionKeys.UserManage, me.Permissions);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_the_same_outcome()
    {
        var wrong = await Auth().LoginAsync("writer@example.com", "not the password", null);
        var unknown = await Auth().LoginAsync("nobody@example.com", "not the password", null);

        Assert.Equal(LoginOutcome.InvalidCredentials, wrong.Outcome);
        Assert.Equal(LoginOutcome.InvalidCredentials, unknown.Outcome);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_even_for_the_correct_password()
    {
        for (var i = 0; i < 5; i++)
            await Auth().LoginAsync("writer@example.com", "wrong", null);

        var locked = await Auth().LoginAsync("writer@example.com", Password, null);
        Assert.Equal(LoginOutcome.Locked, locked.Outcome);

        _now = _now.AddMinutes(16);
        var after = await Auth().LoginAsync("writer@example.com", Password, null);
        Assert.Equal(LoginOutcome.Success, after.Outcome);
    }

    [Fact]
    public async Task Inactive_account_cannot_log_in()
    {
        var user = await _db.Users.SingleAsync();
        user.IsActive = false;
        await _db.SaveChangesAsync();

        var result = await Auth().LoginAsync("writer@example.com", Password, null);
        Assert.Equal(LoginOutcome.Inactive, result.Outcome);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_cannot_be_reused()
    {
        var first = (await Auth().LoginAsync("writer@example.com", Password, null)).Tokens!;

        var second = await Auth().RefreshAsync(first.RefreshToken, null);
        Assert.NotNull(second);
        Assert.NotEqual(first.RefreshToken, second!.RefreshToken);

        // Presenting the rotated token again is treated as theft: every session is revoked.
        Assert.Null(await Auth().RefreshAsync(first.RefreshToken, null));
        Assert.Null(await Auth().RefreshAsync(second.RefreshToken, null));
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var tokens = (await Auth().LoginAsync("writer@example.com", Password, null)).Tokens!;
        _now = _now.AddDays(15);
        Assert.Null(await Auth().RefreshAsync(tokens.RefreshToken, null));
    }

    [Fact]
    public async Task Refresh_tokens_are_stored_hashed_never_in_plain_text()
    {
        var tokens = (await Auth().LoginAsync("writer@example.com", Password, null)).Tokens!;
        var stored = await _db.RefreshTokens.SingleAsync();
        Assert.NotEqual(tokens.RefreshToken, stored.TokenHash);
        Assert.DoesNotContain(tokens.RefreshToken, stored.TokenHash);
    }

    [Fact]
    public async Task Login_attempts_are_written_to_the_audit_log()
    {
        await Auth().LoginAsync("writer@example.com", "wrong", null);
        await Auth().LoginAsync("writer@example.com", Password, null);

        var actions = await _db.AuditLogs.Select(a => a.Action).ToListAsync();
        Assert.Contains("auth.login.failed", actions);
        Assert.Contains("auth.login.succeeded", actions);
    }

    [Fact]
    public async Task Duplicate_email_is_refused_and_unknown_role_is_refused()
    {
        var admin = new UserAdmin(_db, _clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateUserAsync("WRITER@example.com", "Dup", Password, [RoleNames.Publisher]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            admin.CreateUserAsync("new@example.com", "New", Password, ["Superuser"]));
    }

    [Fact]
    public async Task Short_password_is_refused()
    {
        var admin = new UserAdmin(_db, _clock);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            admin.CreateUserAsync("short@example.com", "Short", "short", [RoleNames.Publisher]));
    }

    private sealed class FakeClock(Func<DateTimeOffset> now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now();
    }
}
