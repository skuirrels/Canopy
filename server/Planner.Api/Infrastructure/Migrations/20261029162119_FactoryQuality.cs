using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcceptanceCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    CriteriaJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptanceCriteria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CriterionVerdicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    RunId = table.Column<Guid>(type: "UUID", nullable: false),
                    CriterionId = table.Column<Guid>(type: "UUID", nullable: false),
                    RecordedRevision = table.Column<long>(type: "BIGINT", nullable: false),
                    Verdict = table.Column<string>(type: "VARCHAR", nullable: false),
                    Reason = table.Column<string>(type: "VARCHAR", nullable: false),
                    InputHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    SourceHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    ContextJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    EvidenceJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriterionVerdicts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VerificationReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RunId = table.Column<Guid>(type: "UUID", nullable: false),
                    StepRunId = table.Column<Guid>(type: "UUID", nullable: false),
                    BeforeJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    AfterJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    Error = table.Column<string>(type: "VARCHAR", nullable: true),
                    EvidenceHash = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriterionVerdicts_RequestId",
                table: "CriterionVerdicts",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CriterionVerdicts_RunId_RecordedRevision",
                table: "CriterionVerdicts",
                columns: new[] { "RunId", "RecordedRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationReceipts_StepRunId",
                table: "VerificationReceipts",
                column: "StepRunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptanceCriteria");

            migrationBuilder.DropTable(
                name: "CriterionVerdicts");

            migrationBuilder.DropTable(
                name: "VerificationReceipts");
        }
    }
}
