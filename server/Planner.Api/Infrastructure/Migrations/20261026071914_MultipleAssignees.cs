using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultipleAssignees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    PersonId = table.Column<Guid>(type: "UUID", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_ItemId_PersonId",
                table: "Assignments",
                columns: new[] { "ItemId", "PersonId" },
                unique: true);
            // Match the existing aggregate model: relationships are enforced in Domain,
            // without physical DuckDB FKs that prevent updates to referenced task rows.
            // Retain the legacy nullable column solely for migration rollback. Runtime
            // reads and writes use Assignments; it is never a second source of truth.
            migrationBuilder.Sql("INSERT INTO \"Assignments\" (\"Id\", \"ItemId\", \"PersonId\", \"Position\") SELECT uuid(), \"Id\", \"AssigneeId\", 0 FROM \"Items\" WHERE \"AssigneeId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Items\" SET \"AssigneeId\" = (SELECT \"PersonId\" FROM \"Assignments\" WHERE \"ItemId\" = \"Items\".\"Id\" ORDER BY \"Position\" LIMIT 1)");
            migrationBuilder.DropTable(name: "Assignments");
        }
    }
}
