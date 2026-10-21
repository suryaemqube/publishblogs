using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Move accounts onto the surviving roles before the DeleteData calls below remove roles 1, 2 and 4:
            // UserRoles.RoleId cascades on delete, so anything still pointed at them would be lost silently.
            // Step 1 drops rows that would collide with a role the account already holds; step 2 collapses the
            // case of one account holding both Writer and Reviewer; step 3 moves what is left.
            migrationBuilder.Sql("""
                DELETE FROM UserRoles WHERE RoleId IN (1, 2) AND UserId IN (SELECT UserId FROM UserRoles WHERE RoleId = 3);
                DELETE FROM UserRoles WHERE RoleId = 4 AND UserId IN (SELECT UserId FROM UserRoles WHERE RoleId = 5);
                DELETE FROM UserRoles WHERE RoleId = 2 AND UserId IN (SELECT UserId FROM UserRoles WHERE RoleId = 1);
                UPDATE UserRoles SET RoleId = 3 WHERE RoleId IN (1, 2);
                UPDATE UserRoles SET RoleId = 5 WHERE RoleId = 4;
                """);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 2, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 6, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 7, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 3, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 7, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 7, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 8, 4 });

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 3 },
                    { 2, 3 },
                    { 3, 3 }
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Uploads documents, reviews them and publishes the drafts.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "Everything, including client sites and team management.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverses the role and grant changes only. The accounts that were folded into Publisher stay on
            // it: the old split (who was a Writer and who a Reviewer) was never recorded anywhere to restore.

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 1, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 2, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 3, 3 });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 6, 1 },
                    { 7, 1 },
                    { 3, 2 },
                    { 7, 2 },
                    { 7, 4 },
                    { 8, 4 }
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Publishes approved articles.");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "All permissions, including user management.");

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Description", "IsSystem", "Name" },
                values: new object[,]
                {
                    { 1, "Uploads documents, edits articles, creates drafts.", true, "Writer" },
                    { 2, "Reviews and approves articles.", true, "Reviewer" },
                    { 4, "Configures client sites.", true, "Client Manager" }
                });
        }
    }
}
