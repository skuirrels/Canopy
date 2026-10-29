using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryRunPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FactoryRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    SnapshotJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactoryStageRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RunId = table.Column<Guid>(type: "UUID", nullable: false),
                    StageId = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryStageRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactoryStepRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RunId = table.Column<Guid>(type: "UUID", nullable: false),
                    StageRunId = table.Column<Guid>(type: "UUID", nullable: false),
                    StepId = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryStepRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryRuns_ItemId",
                table: "FactoryRuns",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FactoryRuns_RequestId",
                table: "FactoryRuns",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactoryStageRuns_RunId_StageId",
                table: "FactoryStageRuns",
                columns: new[] { "RunId", "StageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactoryStepRuns_StageRunId_StepId",
                table: "FactoryStepRuns",
                columns: new[] { "StageRunId", "StepId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactoryRuns");

            migrationBuilder.DropTable(
                name: "FactoryStageRuns");

            migrationBuilder.DropTable(
                name: "FactoryStepRuns");
        }
    }
}
