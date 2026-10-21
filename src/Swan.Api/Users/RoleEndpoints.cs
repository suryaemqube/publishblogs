using Microsoft.EntityFrameworkCore;
using Swan.Domain.Identity;
using Swan.Infrastructure.Persistence;

namespace Swan.Api.Users;

/// <summary>
/// The seeded roles and the permission each one carries (PLAN-v2 §11.1, §12). Read-only: the mapping is
/// migration-seeded so it is identical everywhere, and the Team screen only needs to show it.
/// </summary>
public static class RoleEndpoints
{
    public static void MapRoleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/roles").RequireAuthorization(PermissionKeys.UserManage);

        group.MapGet("/", async (SwanDbContext db, CancellationToken ct) =>
        {
            var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);
            var keys = await db.Permissions.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Key, ct);
            var grants = await db.RolePermissions.AsNoTracking().ToListAsync(ct);

            return Results.Ok(roles.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                description = r.Description,
                isSystem = r.IsSystem,
                permissions = grants
                    .Where(g => g.RoleId == r.Id)
                    .Select(g => keys.GetValueOrDefault(g.PermissionId))
                    .Where(k => k is not null)
                    .Select(k => k!)
                    .OrderBy(k => k, StringComparer.Ordinal)
                    .ToArray(),
            }));
        });
    }
}
