using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryAnalyticsIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalyticsReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    SourceId = table.Column<Guid>(type: "UUID", nullable: false),
                    CoverageJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsReportIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    Scope = table.Column<string>(type: "VARCHAR", nullable: false),
                    ReportId = table.Column<string>(type: "VARCHAR", nullable: false),
                    ReportHash = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsReportIdentities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReceipts_ProjectId",
                table: "AnalyticsReceipts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReportIdentities_ProjectId_Scope_ReportId",
                table: "AnalyticsReportIdentities",
                columns: new[] { "ProjectId", "Scope", "ReportId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsReceipts");

            migrationBuilder.DropTable(
                name: "AnalyticsReportIdentities");
        }
    }
}
