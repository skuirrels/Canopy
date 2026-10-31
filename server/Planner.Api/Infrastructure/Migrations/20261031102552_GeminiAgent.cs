using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations;

public partial class GeminiAgent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, true);
    protected override void Down(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, false);

    private static void Rebuild(MigrationBuilder migrationBuilder, bool addGemini)
    {
        // Preserve checkpointed/compressed native DuckDB rows with a transactional
        // table rebuild, as in AgentModelEffort, rather than ALTER ADD COLUMN.
        var geminiColumn = addGemini ? "\"GeminiModel\" VARCHAR NULL," : "";
        const string columns = """
            "Id", "ProjectId", "RepositoryPath", "BaseRef", "Provider",
            "CodexModel", "CodexEffort", "ClaudeModel", "ClaudeEffort", "Instructions"
            """;
        migrationBuilder.Sql($"""
            CREATE TABLE "AgentConnections_GeminiMigration" (
                "Id" UUID NOT NULL, "ProjectId" UUID NOT NULL, "RepositoryPath" VARCHAR NOT NULL,
                "BaseRef" VARCHAR NOT NULL, "Provider" VARCHAR NOT NULL,
                "CodexModel" VARCHAR NULL, "CodexEffort" VARCHAR NULL,
                "ClaudeModel" VARCHAR NULL, "ClaudeEffort" VARCHAR NULL,
                {geminiColumn}
                "Instructions" VARCHAR NOT NULL,
                CONSTRAINT "PK_AgentConnections" PRIMARY KEY ("Id")
            );
            INSERT INTO "AgentConnections_GeminiMigration" ({columns}) SELECT {columns} FROM "AgentConnections";
            DROP TABLE "AgentConnections";
            ALTER TABLE "AgentConnections_GeminiMigration" RENAME TO "AgentConnections";
            CREATE UNIQUE INDEX "IX_AgentConnections_ProjectId" ON "AgentConnections" ("ProjectId");
            """);
    }
}
