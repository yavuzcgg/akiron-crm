using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akiron.Modules.Jobs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderSourceQuote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_quote_id",
                schema: "jobs",
                table: "work_orders",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_quote_id",
                schema: "jobs",
                table: "work_orders");
        }
    }
}
