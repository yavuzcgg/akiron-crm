using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akiron.Modules.Jobs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderFinancials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "budget",
                schema: "jobs",
                table: "work_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cost_per_hour",
                schema: "jobs",
                table: "time_entries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "budget",
                schema: "jobs",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "cost_per_hour",
                schema: "jobs",
                table: "time_entries");
        }
    }
}
