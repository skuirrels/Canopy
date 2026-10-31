using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryErrorFeedReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErrorEventIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    Scope = table.Column<string>(type: "VARCHAR", nullable: false),
                    EventId = table.Column<string>(type: "VARCHAR", nullable: false),
                    GroupId = table.Column<string>(type: "VARCHAR", nullable: false),
                    FactHash = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorEventIdentities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErrorFeedReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    SourceId = table.Column<Guid>(type: "UUID", nullable: false),
                    CoverageJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorFeedReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorEventIdentities_ProjectId_Scope_EventId",
                table: "ErrorEventIdentities",
                columns: new[] { "ProjectId", "Scope", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorFeedReceipts_ProjectId",
                table: "ErrorFeedReceipts",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorEventIdentities");

            migrationBuilder.DropTable(
                name: "ErrorFeedReceipts");
        }
    }
}
