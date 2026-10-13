using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHiddenDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenUtc",
                table: "ArticleDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "ArticleDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenUtc",
                table: "ArticleDocuments");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "ArticleDocuments");
        }
    }
}
