using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FactoryDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    RunId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    Revision = table.Column<long>(type: "BIGINT", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    Reason = table.Column<string>(type: "VARCHAR", nullable: false),
                    CurrentOperationId = table.Column<Guid>(type: "UUID", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    PolicyJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    CaptureJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    RemoteJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    ApprovalsJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    ObservationJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    HookJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "UUID", nullable: false),
                    RequestId = table.Column<Guid>(type: "UUID", nullable: false),
                    Kind = table.Column<string>(type: "VARCHAR", nullable: false),
                    ApprovalNote = table.Column<string>(type: "VARCHAR", nullable: false),
                    RequestHash = table.Column<string>(type: "VARCHAR", nullable: false),
                    State = table.Column<string>(type: "VARCHAR", nullable: false),
                    Error = table.Column<string>(type: "VARCHAR", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: true),
                    ResultJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    PolicyJson = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_ItemId",
                table: "Deliveries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_RequestId",
                table: "Deliveries",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOperations_DeliveryId",
                table: "DeliveryOperations",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOperations_RequestId",
                table: "DeliveryOperations",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "DeliveryOperations");

            migrationBuilder.DropTable(
                name: "DeliveryPolicies");
        }
    }
}
