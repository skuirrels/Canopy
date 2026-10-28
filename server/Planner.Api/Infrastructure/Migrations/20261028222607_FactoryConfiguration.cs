using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FactoryPipelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    Name = table.Column<string>(type: "VARCHAR", nullable: false),
                    Description = table.Column<string>(type: "VARCHAR", nullable: false),
                    Source = table.Column<string>(type: "VARCHAR", nullable: false),
                    DefinitionJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    DraftRevision = table.Column<long>(type: "BIGINT", nullable: false),
                    PublishedDraftRevision = table.Column<long>(type: "BIGINT", nullable: false),
                    Archived = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoryPipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    PipelineId = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "VARCHAR", nullable: false),
                    Description = table.Column<string>(type: "VARCHAR", nullable: false),
                    Source = table.Column<string>(type: "VARCHAR", nullable: false),
                    DefinitionJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskFactoryConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    PipelineVersionId = table.Column<Guid>(type: "UUID", nullable: false),
                    OverridesJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskFactoryConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactoryPipelines_ProjectId",
                table: "FactoryPipelines",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineVersions_PipelineId_Number",
                table: "PipelineVersions",
                columns: new[] { "PipelineId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskFactoryConfigurations_ProjectId",
                table: "TaskFactoryConfigurations",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactoryPipelines");

            migrationBuilder.DropTable(
                name: "PipelineVersions");

            migrationBuilder.DropTable(
                name: "TaskFactoryConfigurations");
        }
    }
}
