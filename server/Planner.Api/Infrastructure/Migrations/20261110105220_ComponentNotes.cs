using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComponentNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    Title = table.Column<string>(type: "VARCHAR", nullable: false),
                    HeldOn = table.Column<DateOnly>(type: "DATE", nullable: true),
                    Markdown = table.Column<string>(type: "VARCHAR", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_ItemId",
                table: "Notes",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notes");
        }
    }
}
