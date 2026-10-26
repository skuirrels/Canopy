using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComponentSiblingOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Items_ProjectId", table: "Items");
            migrationBuilder.AddColumn<int>(
                name: "SiblingPosition",
                table: "Items",
                type: "INTEGER",
                nullable: true,
                defaultValue: 0);
            // DuckDB requires adding the column and its NOT NULL constraint separately.
            migrationBuilder.Sql("ALTER TABLE \"Items\" ALTER COLUMN \"SiblingPosition\" SET NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_Items_ProjectId", table: "Items", column: "ProjectId");
            // Complete schema/index changes before updating existing compressed row groups.
            migrationBuilder.Sql("UPDATE \"Items\" SET \"SiblingPosition\" = \"Position\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SiblingPosition",
                table: "Items");
        }
    }
}
