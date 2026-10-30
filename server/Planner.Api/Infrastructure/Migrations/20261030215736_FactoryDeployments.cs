using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryDeployments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeploymentOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    Kind = table.Column<string>(type: "VARCHAR", nullable: false),
                    ApprovalNote = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    Error = table.Column<string>(type: "VARCHAR", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    ResultJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    Revision = table.Column<long>(type: "BIGINT", nullable: false),
                    TargetId = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    Reason = table.Column<string>(type: "VARCHAR", nullable: false),
                    CurrentOperationId = table.Column<Guid>(type: "UUID", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    CaptureJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    ApprovalsJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    DeploymentJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    RecoveryJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deployments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentOperations_DeploymentId",
                table: "DeploymentOperations",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentOperations_RequestId",
                table: "DeploymentOperations",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_DeliveryId",
                table: "Deployments",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_RequestId",
                table: "Deployments",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentOperations");

            migrationBuilder.DropTable(
                name: "Deployments");
        }
    }
}
