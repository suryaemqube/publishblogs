using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceCandidateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ContentHtml = table.Column<string>(type: "TEXT", nullable: false),
                    SeoTitle = table.Column<string>(type: "TEXT", nullable: true),
                    MetaDescription = table.Column<string>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CanonicalPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsUnverified = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticlePublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArticleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SiteId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RemotePostId = table.Column<long>(type: "INTEGER", nullable: true),
                    RemoteStatus = table.Column<string>(type: "TEXT", nullable: true),
                    RemoteLink = table.Column<string>(type: "TEXT", nullable: true),
                    EditUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticlePublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticlePublications_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticlePublications_ArticleId_SiteId",
                table: "ArticlePublications",
                columns: new[] { "ArticleId", "SiteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_SourceCandidateId",
                table: "Articles",
                column: "SourceCandidateId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticlePublications");

            migrationBuilder.DropTable(
                name: "Articles");
        }
    }
}
