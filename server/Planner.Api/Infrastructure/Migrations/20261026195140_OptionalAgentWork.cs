using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptionalAgentWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    RepositoryPath = table.Column<string>(type: "VARCHAR", nullable: false),
                    BaseRef = table.Column<string>(type: "VARCHAR", nullable: false),
                    Provider = table.Column<string>(type: "VARCHAR", nullable: false),
                    Model = table.Column<string>(type: "VARCHAR", nullable: true),
                    Instructions = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    PreviousRunId = table.Column<Guid>(type: "UUID", nullable: true),
                    Provider = table.Column<string>(type: "VARCHAR", nullable: false),
                    Purpose = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    Title = table.Column<string>(type: "VARCHAR", nullable: false),
                    SpecJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    ProjectRevision = table.Column<long>(type: "BIGINT", nullable: false),
                    RepositoryPath = table.Column<string>(type: "VARCHAR", nullable: false),
                    BaseRef = table.Column<string>(type: "VARCHAR", nullable: false),
                    Model = table.Column<string>(type: "VARCHAR", nullable: true),
                    ProjectInstructions = table.Column<string>(type: "VARCHAR", nullable: false),
                    Instructions = table.Column<string>(type: "VARCHAR", nullable: false),
                    SessionId = table.Column<string>(type: "VARCHAR", nullable: true),
                    WorkspacePath = table.Column<string>(type: "VARCHAR", nullable: true),
                    BaseCommit = table.Column<string>(type: "VARCHAR", nullable: true),
                    Output = table.Column<string>(type: "VARCHAR", nullable: false),
                    Result = table.Column<string>(type: "VARCHAR", nullable: false),
                    Diff = table.Column<string>(type: "VARCHAR", nullable: false),
                    Error = table.Column<string>(type: "VARCHAR", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentConnections_ProjectId",
                table: "AgentConnections",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_ItemId",
                table: "AgentRuns",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_RequestId",
                table: "AgentRuns",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentConnections");

            migrationBuilder.DropTable(
                name: "AgentRuns");
        }
    }
}
