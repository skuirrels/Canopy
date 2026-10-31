using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactorySkillCalibration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CalibrationJobId",
                table: "AgentRuns",
                type: "UUID",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalibrationPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    PolicyJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Calibrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "UUID", nullable: false),
                    RunId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    PolicyHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    Revision = table.Column<long>(type: "BIGINT", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    Error = table.Column<string>(type: "VARCHAR", nullable: true),
                    WorkspacePath = table.Column<string>(type: "VARCHAR", nullable: true),
                    InputJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "UUID", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    CaptureJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    EditsJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    WritesJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    ActionsJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calibrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationTriggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    PolicyHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    ScopeHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    JobId = table.Column<Guid>(type: "UUID", nullable: true),
                    Error = table.Column<string>(type: "VARCHAR", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationTriggers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Calibrations_DeliveryId_PolicyHash",
                table: "Calibrations",
                columns: new[] { "DeliveryId", "PolicyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Calibrations_RequestId",
                table: "Calibrations",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationTriggers_DeliveryId_PolicyHash_ScopeHash",
                table: "CalibrationTriggers",
                columns: new[] { "DeliveryId", "PolicyHash", "ScopeHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalibrationPolicies");

            migrationBuilder.DropTable(
                name: "Calibrations");

            migrationBuilder.DropTable(
                name: "CalibrationTriggers");

            migrationBuilder.DropColumn(
                name: "CalibrationJobId",
                table: "AgentRuns");
        }
    }
}
