using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planner.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ObservedAgentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObservedModel",
                table: "AgentRuns",
                type: "VARCHAR",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObservedModel",
                table: "AgentRuns");
        }
    }
}
