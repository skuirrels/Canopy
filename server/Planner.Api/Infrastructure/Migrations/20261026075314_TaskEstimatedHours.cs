using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations;

public partial class TaskEstimatedHours : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => RebuildItems(migrationBuilder, includeEstimate: true);
    protected override void Down(MigrationBuilder migrationBuilder) => RebuildItems(migrationBuilder, includeEstimate: false);

    private static void RebuildItems(MigrationBuilder migrationBuilder, bool includeEstimate)
    {
        // ALTER ADD on this native DuckDB version crashes at commit for persisted,
        // compressed Items rows. Rebuild transactionally instead, preserving every
        // original column (including the legacy assignee column for rollback).
        // Existing tasks have no estimate; neither dates nor assignments infer one.
        var estimateColumn = includeEstimate ? "\"EstimatedHours\" DOUBLE NULL," : "";
        migrationBuilder.Sql($"""
            CREATE TABLE "Items_EstimateMigration" (
                "Id" UUID NOT NULL, "ProjectId" UUID NOT NULL, "ParentId" UUID NULL,
                "StatusId" UUID NOT NULL, "Title" VARCHAR NOT NULL, "SpecJson" VARCHAR NOT NULL,
                "Priority" VARCHAR NOT NULL, "AssigneeId" UUID NULL,
                "StartDate" DATE NULL, "DueDate" DATE NULL, "Position" INTEGER NOT NULL,
                "X" DOUBLE NULL, "Y" DOUBLE NULL,
                "CreatedAt" TIMESTAMP NOT NULL, "UpdatedAt" TIMESTAMP NOT NULL,
                "SiblingPosition" INTEGER NOT NULL DEFAULT 0,
                {estimateColumn}
                CONSTRAINT "PK_Items" PRIMARY KEY ("Id")
            );
            INSERT INTO "Items_EstimateMigration"
                ("Id", "ProjectId", "ParentId", "StatusId", "Title", "SpecJson", "Priority", "AssigneeId",
                 "StartDate", "DueDate", "Position", "X", "Y", "CreatedAt", "UpdatedAt", "SiblingPosition")
            SELECT "Id", "ProjectId", "ParentId", "StatusId", "Title", "SpecJson", "Priority", "AssigneeId",
                   "StartDate", "DueDate", "Position", "X", "Y", "CreatedAt", "UpdatedAt", "SiblingPosition"
            FROM "Items";
            DROP TABLE "Items";
            ALTER TABLE "Items_EstimateMigration" RENAME TO "Items";
            CREATE INDEX "IX_Items_ProjectId" ON "Items" ("ProjectId");
            """);
    }
}
