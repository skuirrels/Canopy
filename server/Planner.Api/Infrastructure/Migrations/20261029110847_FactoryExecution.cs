using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Planner.Api.Infrastructure.Migrations;

public partial class FactoryExecution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, true);
    protected override void Down(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, false);

    private static void Rebuild(MigrationBuilder migrationBuilder, bool execution)
    {
        // DuckDB rejects ALTER ADD with constraints and has persisted-row ALTER
        // limitations. Match the existing native migration pattern: copy every
        // old column transactionally, rename, and recreate the existing indexes.
        var runColumns = execution ? """
            "Revision" BIGINT NOT NULL DEFAULT 1, "StartRequestId" UUID NULL,
            "WorkspacePath" VARCHAR NULL, "Error" VARCHAR NULL,
            "StartedAt" TIMESTAMP NULL, "FinishedAt" TIMESTAMP NULL,
            """ : "";
        var stageColumns = execution ? "\"StartedAt\" TIMESTAMP NULL, \"FinishedAt\" TIMESTAMP NULL," : "";
        var stepColumns = execution ? """
            "AgentRunId" UUID NULL, "DecisionRequestId" UUID NULL, "Decision" BOOLEAN NULL, "DecisionNote" VARCHAR NULL,
            "Output" VARCHAR NOT NULL DEFAULT '', "OutputLimitReached" BOOLEAN NOT NULL DEFAULT false,
            "Evidence" VARCHAR NOT NULL DEFAULT '', "Error" VARCHAR NULL, "ExitCode" INTEGER NULL,
            "StartedAt" TIMESTAMP NULL, "FinishedAt" TIMESTAMP NULL,
            """ : "";
        var agentColumns = execution ? "\"FactoryStepRunId\" UUID NULL, \"FactoryPermission\" VARCHAR NULL," : "";
        const string agentCopy = """
            "Id", "RequestId", "ProjectId", "ItemId", "PreviousRunId", "Provider", "Purpose", "State",
            "Title", "SpecJson", "ProjectRevision", "RepositoryPath", "BaseRef", "Model", "Effort", "RequestedModel", "RequestedEffort",
            "ProjectInstructions", "Instructions", "SessionId", "WorkspacePath", "BaseCommit", "Output", "Result", "Diff", "Error", "CreatedAt", "StartedAt", "FinishedAt"
            """;
        if (!execution) migrationBuilder.DropTable("FactoryArtifacts");
        migrationBuilder.Sql($"""
            CREATE TABLE "FactoryRuns_ExecutionMigration" (
                "Id" UUID NOT NULL, "RequestId" UUID NOT NULL, "ProjectId" UUID NOT NULL, "ItemId" UUID NOT NULL,
                "State" VARCHAR NOT NULL, "SnapshotJson" VARCHAR NOT NULL, "CreatedAt" TIMESTAMP NOT NULL,
                {runColumns}
                CONSTRAINT "PK_FactoryRuns" PRIMARY KEY ("Id")
            );
            INSERT INTO "FactoryRuns_ExecutionMigration" ("Id", "RequestId", "ProjectId", "ItemId", "State", "SnapshotJson", "CreatedAt")
            SELECT "Id", "RequestId", "ProjectId", "ItemId", "State", "SnapshotJson", "CreatedAt" FROM "FactoryRuns";
            DROP TABLE "FactoryRuns";
            ALTER TABLE "FactoryRuns_ExecutionMigration" RENAME TO "FactoryRuns";
            CREATE UNIQUE INDEX "IX_FactoryRuns_RequestId" ON "FactoryRuns" ("RequestId");
            CREATE INDEX "IX_FactoryRuns_ItemId" ON "FactoryRuns" ("ItemId");

            CREATE TABLE "FactoryStageRuns_ExecutionMigration" (
                "Id" UUID NOT NULL, "RunId" UUID NOT NULL, "StageId" VARCHAR NOT NULL, "State" VARCHAR NOT NULL,
                {stageColumns}
                CONSTRAINT "PK_FactoryStageRuns" PRIMARY KEY ("Id")
            );
            INSERT INTO "FactoryStageRuns_ExecutionMigration" ("Id", "RunId", "StageId", "State") SELECT "Id", "RunId", "StageId", "State" FROM "FactoryStageRuns";
            DROP TABLE "FactoryStageRuns";
            ALTER TABLE "FactoryStageRuns_ExecutionMigration" RENAME TO "FactoryStageRuns";
            CREATE UNIQUE INDEX "IX_FactoryStageRuns_RunId_StageId" ON "FactoryStageRuns" ("RunId", "StageId");

            CREATE TABLE "FactoryStepRuns_ExecutionMigration" (
                "Id" UUID NOT NULL, "RunId" UUID NOT NULL, "StageRunId" UUID NOT NULL, "StepId" VARCHAR NOT NULL, "State" VARCHAR NOT NULL,
                {stepColumns}
                CONSTRAINT "PK_FactoryStepRuns" PRIMARY KEY ("Id")
            );
            INSERT INTO "FactoryStepRuns_ExecutionMigration" ("Id", "RunId", "StageRunId", "StepId", "State") SELECT "Id", "RunId", "StageRunId", "StepId", "State" FROM "FactoryStepRuns";
            DROP TABLE "FactoryStepRuns";
            ALTER TABLE "FactoryStepRuns_ExecutionMigration" RENAME TO "FactoryStepRuns";
            CREATE UNIQUE INDEX "IX_FactoryStepRuns_StageRunId_StepId" ON "FactoryStepRuns" ("StageRunId", "StepId");

            CREATE TABLE "AgentRuns_ExecutionMigration" (
                "Id" UUID NOT NULL, "RequestId" UUID NOT NULL, "ProjectId" UUID NOT NULL, "ItemId" UUID NOT NULL,
                "PreviousRunId" UUID NULL, "Provider" VARCHAR NOT NULL, "Purpose" VARCHAR NOT NULL, "State" VARCHAR NOT NULL,
                "Title" VARCHAR NOT NULL, "SpecJson" VARCHAR NOT NULL, "ProjectRevision" BIGINT NOT NULL,
                "RepositoryPath" VARCHAR NOT NULL, "BaseRef" VARCHAR NOT NULL, "Model" VARCHAR NULL,
                "Effort" VARCHAR NULL, "RequestedModel" VARCHAR NULL, "RequestedEffort" VARCHAR NULL,
                "ProjectInstructions" VARCHAR NOT NULL, "Instructions" VARCHAR NOT NULL,
                "SessionId" VARCHAR NULL, "WorkspacePath" VARCHAR NULL, "BaseCommit" VARCHAR NULL,
                "Output" VARCHAR NOT NULL, "Result" VARCHAR NOT NULL, "Diff" VARCHAR NOT NULL, "Error" VARCHAR NULL,
                "CreatedAt" TIMESTAMP NOT NULL, "StartedAt" TIMESTAMP NULL, "FinishedAt" TIMESTAMP NULL,
                {agentColumns}
                CONSTRAINT "PK_AgentRuns" PRIMARY KEY ("Id")
            );
            INSERT INTO "AgentRuns_ExecutionMigration" ({agentCopy}) SELECT {agentCopy} FROM "AgentRuns";
            DROP TABLE "AgentRuns";
            ALTER TABLE "AgentRuns_ExecutionMigration" RENAME TO "AgentRuns";
            CREATE UNIQUE INDEX "IX_AgentRuns_RequestId" ON "AgentRuns" ("RequestId");
            CREATE INDEX "IX_AgentRuns_ItemId" ON "AgentRuns" ("ItemId");
            """);
        if (execution) migrationBuilder.Sql("""
            CREATE UNIQUE INDEX "IX_AgentRuns_FactoryStepRunId" ON "AgentRuns" ("FactoryStepRunId");
            CREATE TABLE "FactoryArtifacts" (
                "Id" UUID NOT NULL, "RunId" UUID NOT NULL, "StepRunId" UUID NOT NULL,
                "Name" VARCHAR NOT NULL, "Kind" VARCHAR NOT NULL, "EvidenceHash" VARCHAR NOT NULL,
                CONSTRAINT "PK_FactoryArtifacts" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX "IX_FactoryArtifacts_RunId_Name" ON "FactoryArtifacts" ("RunId", "Name");
            """);
    }
}
