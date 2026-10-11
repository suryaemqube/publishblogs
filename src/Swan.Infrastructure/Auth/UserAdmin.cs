using Microsoft.EntityFrameworkCore;
using Swan.Domain.Identity;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Security;

namespace Swan.Infrastructure.Auth;

public sealed class UserAdmin(SwanDbContext db, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public const int MinPasswordLength = 12;

    /// <summary>Creates an active local account with one or more named roles. Fails on duplicate email or unknown role.</summary>
    public async Task<User> CreateUserAsync(string email, string displayName, string password, IEnumerable<string> roleNames, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (!normalized.Contains('@')) throw new ArgumentException("Email address is not valid.");
        if (password.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");
        if (await db.Users.AnyAsync(u => u.Email == normalized, ct))
            throw new InvalidOperationException($"An account for '{normalized}' already exists.");

        var roles = await db.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync(ct);
        var missing = roleNames.Except(roles.Select(r => r.Name)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException($"Unknown role(s): {string.Join(", ", missing)}.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            DisplayName = displayName.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            IsActive = true,
            CreatedUtc = _clock.GetUtcNow().UtcDateTime,
        };
        db.Users.Add(user);
        db.UserRoles.AddRange(roles.Select(r => new UserRole { UserId = user.Id, RoleId = r.Id }));
        db.AuditLogs.Add(new AuditLog
        {
            UserId = null,
            Action = "user.created",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            Details = string.Join(",", roles.Select(r => r.Name)),
            CreatedUtc = user.CreatedUtc,
        });
        await db.SaveChangesAsync(ct);
        return user;
    }
}
