using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalPath",
                table: "ArticleCandidates",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "ArticleCandidates",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoTitle",
                table: "ArticleCandidates",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoWarnings",
                table: "ArticleCandidates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "ArticleCandidates",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanonicalPath",
                table: "ArticleCandidates");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "ArticleCandidates");

            migrationBuilder.DropColumn(
                name: "SeoTitle",
                table: "ArticleCandidates");

            migrationBuilder.DropColumn(
                name: "SeoWarnings",
                table: "ArticleCandidates");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "ArticleCandidates");
        }
    }
}
