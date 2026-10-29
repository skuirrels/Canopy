using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    Fingerprint = table.Column<string>(type: "VARCHAR", nullable: false),
                    ObservationId = table.Column<Guid>(type: "UUID", nullable: false),
                    Revision = table.Column<long>(type: "BIGINT", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    VerifiedHash = table.Column<string>(type: "VARCHAR", nullable: true),
                    TaskId = table.Column<Guid>(type: "UUID", nullable: true),
                    Reason = table.Column<string>(type: "VARCHAR", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    SourceId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    SourceRevision = table.Column<long>(type: "BIGINT", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    Error = table.Column<string>(type: "VARCHAR", nullable: true),
                    Observed = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    CandidateId = table.Column<Guid>(type: "UUID", nullable: false),
                    ObservationId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    Action = table.Column<string>(type: "VARCHAR", nullable: false),
                    Reason = table.Column<string>(type: "VARCHAR", nullable: false),
                    TaskId = table.Column<Guid>(type: "UUID", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    CandidateId = table.Column<Guid>(type: "UUID", nullable: false),
                    CollectionId = table.Column<Guid>(type: "UUID", nullable: false),
                    SignalJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    Revision = table.Column<long>(type: "BIGINT", nullable: false),
                    Scope = table.Column<string>(type: "VARCHAR", nullable: false),
                    ProviderBinding = table.Column<string>(type: "VARCHAR", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeCandidates_ProjectId_Fingerprint",
                table: "IntakeCandidates",
                columns: new[] { "ProjectId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeCollections_RequestId",
                table: "IntakeCollections",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeCollections_SourceId",
                table: "IntakeCollections",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDecisions_CandidateId",
                table: "IntakeDecisions",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDecisions_RequestId",
                table: "IntakeDecisions",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeObservations_CandidateId",
                table: "IntakeObservations",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSources_ProjectId_Scope",
                table: "IntakeSources",
                columns: new[] { "ProjectId", "Scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeCandidates");

            migrationBuilder.DropTable(
                name: "IntakeCollections");

            migrationBuilder.DropTable(
                name: "IntakeDecisions");

            migrationBuilder.DropTable(
                name: "IntakeObservations");

            migrationBuilder.DropTable(
                name: "IntakeSources");
        }
    }
}
