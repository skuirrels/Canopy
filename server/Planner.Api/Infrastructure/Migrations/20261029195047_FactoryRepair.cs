using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepairAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    LoopId = table.Column<Guid>(type: "UUID", nullable: false),
                    RunId = table.Column<Guid>(type: "UUID", nullable: false),
                    ParentRunId = table.Column<Guid>(type: "UUID", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    FeedbackHash = table.Column<string>(type: "VARCHAR", nullable: true),
                    FeedbackJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepairLoops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    RootRunId = table.Column<Guid>(type: "UUID", nullable: false),
                    Revision = table.Column<long>(type: "BIGINT", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    Reason = table.Column<string>(type: "VARCHAR", nullable: false),
                    ReservedAgentInvocations = table.Column<int>(type: "INTEGER", nullable: false),
                    NoProgressRounds = table.Column<int>(type: "INTEGER", nullable: false),
                    RemainingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    PolicyJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    ScopeJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    ContextJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    OperationsJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairLoops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepairPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    PolicyJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepairAttempts_LoopId_Number",
                table: "RepairAttempts",
                columns: new[] { "LoopId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepairAttempts_RunId",
                table: "RepairAttempts",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepairLoops_ItemId",
                table: "RepairLoops",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RepairLoops_RequestId",
                table: "RepairLoops",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepairAttempts");

            migrationBuilder.DropTable(
                name: "RepairLoops");

            migrationBuilder.DropTable(
                name: "RepairPolicies");
        }
    }
}
