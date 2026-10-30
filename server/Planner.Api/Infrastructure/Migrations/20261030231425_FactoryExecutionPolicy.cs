using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryExecutionPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RebuildAgents(migrationBuilder, true);

            migrationBuilder.CreateTable(
                name: "ExecutionPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    PolicyJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionPolicies", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionPolicies");

            RebuildAgents(migrationBuilder, false);
        }
        private static void RebuildAgents(MigrationBuilder migrationBuilder, bool addUsage)
        {
            // The public native engine rejects constrained ALTER ADD. Preserve
            // every existing column and index through the established copy path.
            var usage = addUsage ? "\"UsageJson\" VARCHAR NOT NULL DEFAULT ''," : "";
            const string columns = """
                "Id", "RequestId", "ProjectId", "ItemId", "PreviousRunId", "Provider", "Purpose", "State",
                "Title", "SpecJson", "ProjectRevision", "RepositoryPath", "BaseRef", "Model", "Effort", "RequestedModel", "RequestedEffort",
                "ProjectInstructions", "Instructions", "SessionId", "WorkspacePath", "BaseCommit", "Output", "Result", "Diff", "Error", "CreatedAt", "StartedAt", "FinishedAt", "FactoryStepRunId", "FactoryPermission"
                """;
            migrationBuilder.Sql($"""
                CREATE TABLE "AgentRuns_UsageMigration" (
                    "Id" UUID NOT NULL, "RequestId" UUID NOT NULL, "ProjectId" UUID NOT NULL, "ItemId" UUID NOT NULL,
                    "PreviousRunId" UUID NULL, "Provider" VARCHAR NOT NULL, "Purpose" VARCHAR NOT NULL, "State" VARCHAR NOT NULL,
                    "Title" VARCHAR NOT NULL, "SpecJson" VARCHAR NOT NULL, "ProjectRevision" BIGINT NOT NULL,
                    "RepositoryPath" VARCHAR NOT NULL, "BaseRef" VARCHAR NOT NULL, "Model" VARCHAR NULL,
                    "Effort" VARCHAR NULL, "RequestedModel" VARCHAR NULL, "RequestedEffort" VARCHAR NULL,
                    "ProjectInstructions" VARCHAR NOT NULL, "Instructions" VARCHAR NOT NULL,
                    "SessionId" VARCHAR NULL, "WorkspacePath" VARCHAR NULL, "BaseCommit" VARCHAR NULL,
                    "Output" VARCHAR NOT NULL, "Result" VARCHAR NOT NULL, "Diff" VARCHAR NOT NULL, "Error" VARCHAR NULL,
                    "CreatedAt" TIMESTAMP NOT NULL, "StartedAt" TIMESTAMP NULL, "FinishedAt" TIMESTAMP NULL,
                    "FactoryStepRunId" UUID NULL, "FactoryPermission" VARCHAR NULL,
                    {usage}
                    CONSTRAINT "PK_AgentRuns" PRIMARY KEY ("Id")
                );
                INSERT INTO "AgentRuns_UsageMigration" ({columns}) SELECT {columns} FROM "AgentRuns";
                DROP TABLE "AgentRuns";
                ALTER TABLE "AgentRuns_UsageMigration" RENAME TO "AgentRuns";
                CREATE UNIQUE INDEX "IX_AgentRuns_RequestId" ON "AgentRuns" ("RequestId");
                CREATE INDEX "IX_AgentRuns_ItemId" ON "AgentRuns" ("ItemId");
                CREATE UNIQUE INDEX "IX_AgentRuns_FactoryStepRunId" ON "AgentRuns" ("FactoryStepRunId");
                """);
        }
    }
}
