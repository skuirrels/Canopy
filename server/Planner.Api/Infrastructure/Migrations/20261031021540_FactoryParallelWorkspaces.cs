using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryParallelWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParallelRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    Head = table.Column<string>(type: "VARCHAR", nullable: false),
                    IndexHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    InitialJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    InitialContextJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    FinalJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    FinalContextJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    Error = table.Column<string>(type: "VARCHAR", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParallelRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParallelStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RunId = table.Column<Guid>(type: "UUID", nullable: false),
                    WorkspacePath = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    InputJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    OutputJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    CheckpointJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    StepsJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    Error = table.Column<string>(type: "VARCHAR", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParallelStages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParallelStages_RunId",
                table: "ParallelStages",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParallelRuns");

            migrationBuilder.DropTable(
                name: "ParallelStages");
        }
    }
}
