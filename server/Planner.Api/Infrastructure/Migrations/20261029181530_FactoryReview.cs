using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    RoundId = table.Column<Guid>(type: "UUID", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    DataJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewFindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    RunId = table.Column<Guid>(type: "UUID", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<long>(type: "BIGINT", nullable: false),
                    Reviewer = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    SourceHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    Assessment = table.Column<string>(type: "VARCHAR", nullable: false),
                    AssessmentStepRunId = table.Column<Guid>(type: "UUID", nullable: true),
                    AssessmentEvidenceHash = table.Column<string>(type: "VARCHAR", nullable: true),
                    AbandonReason = table.Column<string>(type: "VARCHAR", nullable: true),
                    PacketJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    BuilderReportsJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    OperationsJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    SubmissionJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRounds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewFindings_ItemId_Number",
                table: "ReviewFindings",
                columns: new[] { "ItemId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRounds_ItemId_Number",
                table: "ReviewRounds",
                columns: new[] { "ItemId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRounds_RequestId",
                table: "ReviewRounds",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewFindings");

            migrationBuilder.DropTable(
                name: "ReviewRounds");
        }
    }
}
