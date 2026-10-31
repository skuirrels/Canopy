using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlanImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    Source = table.Column<string>(type: "VARCHAR", nullable: false),
                    SourceHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    Key = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanImports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanImports_ProjectId_Source_Key",
                table: "PlanImports",
                columns: new[] { "ProjectId", "Source", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanImports");
        }
    }
}
