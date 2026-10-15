using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieFlix.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Query = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedQuery = table.Column<string>(type: "TEXT", nullable: false),
                    SearchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchHistoryEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistoryEntries_UserId_NormalizedQuery",
                table: "SearchHistoryEntries",
                columns: new[] { "UserId", "NormalizedQuery" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistoryEntries_UserId_SearchedAt",
                table: "SearchHistoryEntries",
                columns: new[] { "UserId", "SearchedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchHistoryEntries");
        }
    }
}
