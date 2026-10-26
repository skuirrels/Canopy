using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations;

public partial class AgentModelEffort : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, true);
    protected override void Down(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, false);

    private static void Rebuild(MigrationBuilder migrationBuilder, bool addSettings)
    {
        // Rebuild transactionally rather than ALTER ADD on persisted compressed DuckDB rows.
        // The old model belongs only to the connection's selected provider.
        var connectionModelColumns = addSettings
            ? "\"CodexModel\" VARCHAR NULL, \"CodexEffort\" VARCHAR NULL, \"ClaudeModel\" VARCHAR NULL, \"ClaudeEffort\" VARCHAR NULL,"
            : "\"Model\" VARCHAR NULL,";
        var connectionTargets = addSettings ? "\"CodexModel\", \"ClaudeModel\"" : "\"Model\"";
        var connectionSources = addSettings
            ? "CASE WHEN \"Provider\" = 'codex' THEN \"Model\" END, CASE WHEN \"Provider\" = 'claude' THEN \"Model\" END"
            : "CASE WHEN \"Provider\" = 'codex' THEN \"CodexModel\" ELSE \"ClaudeModel\" END";
        var runSettingsColumns = addSettings ? "\"Effort\" VARCHAR NULL, \"RequestedModel\" VARCHAR NULL, \"RequestedEffort\" VARCHAR NULL," : "";
        const string runColumns = """
            "Id", "RequestId", "ProjectId", "ItemId", "PreviousRunId", "Provider", "Purpose", "State",
            "Title", "SpecJson", "ProjectRevision", "RepositoryPath", "BaseRef", "Model",
            "ProjectInstructions", "Instructions", "SessionId", "WorkspacePath", "BaseCommit",
            "Output", "Result", "Diff", "Error", "CreatedAt", "StartedAt", "FinishedAt"
            """;
        migrationBuilder.Sql($"""
            CREATE TABLE "AgentConnections_SettingsMigration" (
                "Id" UUID NOT NULL, "ProjectId" UUID NOT NULL, "RepositoryPath" VARCHAR NOT NULL,
                "BaseRef" VARCHAR NOT NULL, "Provider" VARCHAR NOT NULL,
                {connectionModelColumns}
                "Instructions" VARCHAR NOT NULL,
                CONSTRAINT "PK_AgentConnections" PRIMARY KEY ("Id")
            );
            INSERT INTO "AgentConnections_SettingsMigration"
                ("Id", "ProjectId", "RepositoryPath", "BaseRef", "Provider", "Instructions", {connectionTargets})
            SELECT "Id", "ProjectId", "RepositoryPath", "BaseRef", "Provider", "Instructions", {connectionSources}
            FROM "AgentConnections";
            DROP TABLE "AgentConnections";
            ALTER TABLE "AgentConnections_SettingsMigration" RENAME TO "AgentConnections";
            CREATE UNIQUE INDEX "IX_AgentConnections_ProjectId" ON "AgentConnections" ("ProjectId");

            CREATE TABLE "AgentRuns_SettingsMigration" (
                "Id" UUID NOT NULL, "RequestId" UUID NOT NULL, "ProjectId" UUID NOT NULL, "ItemId" UUID NOT NULL,
                "PreviousRunId" UUID NULL, "Provider" VARCHAR NOT NULL, "Purpose" VARCHAR NOT NULL, "State" VARCHAR NOT NULL,
                "Title" VARCHAR NOT NULL, "SpecJson" VARCHAR NOT NULL, "ProjectRevision" BIGINT NOT NULL,
                "RepositoryPath" VARCHAR NOT NULL, "BaseRef" VARCHAR NOT NULL, "Model" VARCHAR NULL,
                {runSettingsColumns}
                "ProjectInstructions" VARCHAR NOT NULL, "Instructions" VARCHAR NOT NULL,
                "SessionId" VARCHAR NULL, "WorkspacePath" VARCHAR NULL, "BaseCommit" VARCHAR NULL,
                "Output" VARCHAR NOT NULL, "Result" VARCHAR NOT NULL, "Diff" VARCHAR NOT NULL, "Error" VARCHAR NULL,
                "CreatedAt" TIMESTAMP NOT NULL, "StartedAt" TIMESTAMP NULL, "FinishedAt" TIMESTAMP NULL,
                CONSTRAINT "PK_AgentRuns" PRIMARY KEY ("Id")
            );
            INSERT INTO "AgentRuns_SettingsMigration" ({runColumns}) SELECT {runColumns} FROM "AgentRuns";
            DROP TABLE "AgentRuns";
            ALTER TABLE "AgentRuns_SettingsMigration" RENAME TO "AgentRuns";
            CREATE UNIQUE INDEX "IX_AgentRuns_RequestId" ON "AgentRuns" ("RequestId");
            CREATE INDEX "IX_AgentRuns_ItemId" ON "AgentRuns" ("ItemId");
            """);
    }
}
