using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ItemId = table.Column<Guid>(type: "UUID", nullable: false),
                    DependsOnId = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dependencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    ParentId = table.Column<Guid>(type: "UUID", nullable: true),
                    StatusId = table.Column<Guid>(type: "UUID", nullable: false),
                    Title = table.Column<string>(type: "VARCHAR", nullable: false),
                    SpecJson = table.Column<string>(type: "VARCHAR", nullable: false),
                    Priority = table.Column<string>(type: "VARCHAR", nullable: false),
                    AssigneeId = table.Column<Guid>(type: "UUID", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "DATE", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "DATE", nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<double>(type: "DOUBLE", nullable: true),
                    Y = table.Column<double>(type: "DOUBLE", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    Name = table.Column<string>(type: "VARCHAR", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    Name = table.Column<string>(type: "VARCHAR", nullable: false),
                    Description = table.Column<string>(type: "VARCHAR", nullable: false),
                    Color = table.Column<string>(type: "VARCHAR", nullable: false),
                    Revision = table.Column<long>(type: "BIGINT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMP", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Statuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "UUID", nullable: false),
                    ProjectId = table.Column<Guid>(type: "UUID", nullable: false),
                    Name = table.Column<string>(type: "VARCHAR", nullable: false),
                    Color = table.Column<string>(type: "VARCHAR", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCompleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Statuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dependencies_ProjectId",
                table: "Dependencies",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ProjectId",
                table: "Items",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Statuses_ProjectId",
                table: "Statuses",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dependencies");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Statuses");
        }
    }
}
