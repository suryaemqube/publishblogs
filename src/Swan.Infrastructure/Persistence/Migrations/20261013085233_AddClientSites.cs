using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientSites",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RestOrigin = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    PostTypeKey = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    RestBase = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TaxonomiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    WriteYoastMeta = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserAgent = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConnectionStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LastTestedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastTestSummary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteSecrets",
                columns: table => new
                {
                    SiteId = table.Column<string>(type: "TEXT", nullable: false),
                    ProtectedApplicationPassword = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSecrets", x => x.SiteId);
                    table.ForeignKey(
                        name: "FK_SiteSecrets_ClientSites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "ClientSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteSecrets");

            migrationBuilder.DropTable(
                name: "ClientSites");
        }
    }
}
