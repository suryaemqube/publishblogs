using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TotalParagraphs = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportJson = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedWithOverride = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CandidateIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ParagraphFrom = table.Column<int>(type: "INTEGER", nullable: false),
                    ParagraphTo = table.Column<int>(type: "INTEGER", nullable: false),
                    ProposedTitle = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ContentHtml = table.Column<string>(type: "TEXT", nullable: false),
                    WordCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsUnverified = table.Column<bool>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleCandidates_ArticleDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "ArticleDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCandidates_DocumentId_CandidateIndex",
                table: "ArticleCandidates",
                columns: new[] { "DocumentId", "CandidateIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleDocuments_ContentHash",
                table: "ArticleDocuments",
                column: "ContentHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleCandidates");

            migrationBuilder.DropTable(
                name: "ArticleDocuments");
        }
    }
}
