using System.Security.Claims;
using Swan.Domain.Identity;
using Swan.Infrastructure.Auth;

namespace Swan.Api.Users;

/// <summary>Body of POST /api/users. Fields are nullable so a bad payload answers with a message, not a 400 shape error.</summary>
public sealed record CreateUserBody(string? Email, string? DisplayName, string? Password, string[]? Roles);

/// <summary>
/// User administration for the Team screen (PLAN-v2 §12). Every route needs <c>user.manage</c>, which the seeded
/// permissions grant to Administrator only. Accounts are local; the same accounts the command line creates.
/// </summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization(PermissionKeys.UserManage);

        group.MapGet("/", async (UserAdmin admin, CancellationToken ct) =>
            Results.Ok((await admin.ListAsync(ct)).Select(r => ToDto(r.User, r.Roles))));

        group.MapPost("/", async (CreateUserBody body, UserAdmin admin, ClaimsPrincipal principal,
                                  HttpContext http, CancellationToken ct) =>
        {
            var email = body.Email?.Trim() ?? "";
            var displayName = body.DisplayName?.Trim() ?? "";
            var roles = (body.Roles ?? []).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToArray();

            if (email.Length == 0) return Results.BadRequest(new { error = "An email address is required." });
            if (displayName.Length == 0) return Results.BadRequest(new { error = "A display name is required." });
            if (roles.Length == 0) return Results.BadRequest(new { error = "Choose at least one role." });

            var actor = Guid.TryParse(principal.FindFirstValue("sub"), out var sub) ? sub : (Guid?)null;
            try
            {
                var user = await admin.CreateUserAsync(email, displayName, body.Password ?? "", roles, actor,
                                                       http.Connection.RemoteIpAddress?.ToString(), ct);
                return Results.Created($"/api/users/{user.Id}", ToDto(user, roles));
            }
            catch (ArgumentException ex)
            {
                // Invalid email, password below the minimum length, or an unknown role name.
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                // The email is already in use.
                return Results.Conflict(new { error = ex.Message });
            }
        });
    }

    private static object ToDto(User user, IReadOnlyList<string> roles) => new
    {
        id = user.Id,
        email = user.Email,
        displayName = user.DisplayName,
        roles,
        isActive = user.IsActive,
        lastLoginUtc = user.LastLoginUtc,
        createdUtc = user.CreatedUtc,
    };
}
