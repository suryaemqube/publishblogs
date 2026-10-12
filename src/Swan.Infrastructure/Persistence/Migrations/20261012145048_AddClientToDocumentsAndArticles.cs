using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientToDocumentsAndArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SiteId",
                table: "Articles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteId",
                table: "ArticleDocuments",
                type: "TEXT",
                nullable: true);

            // Backfill from evidence only: an article that already has a WordPress draft belongs to that site.
            // Articles without a draft stay unassigned and must be re-uploaded with a client before they can be sent.
            migrationBuilder.Sql(@"
                UPDATE Articles
                SET SiteId = (SELECT p.SiteId FROM ArticlePublications p WHERE p.ArticleId = Articles.Id
                              ORDER BY p.LastSyncedUtc LIMIT 1)
                WHERE SiteId IS NULL;");

            migrationBuilder.Sql(@"
                UPDATE ArticleDocuments
                SET SiteId = (SELECT a.SiteId FROM Articles a WHERE a.SourceDocumentId = ArticleDocuments.Id
                              AND a.SiteId IS NOT NULL LIMIT 1)
                WHERE SiteId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "ArticleDocuments");
        }
    }
}
