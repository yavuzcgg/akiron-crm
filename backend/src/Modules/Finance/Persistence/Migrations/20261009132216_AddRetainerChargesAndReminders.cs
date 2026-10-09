using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akiron.Modules.Finance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetainerChargesAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_reminded_at",
                schema: "finance",
                table: "open_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reminders_sent",
                schema: "finance",
                table: "open_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "contract_id",
                schema: "finance",
                table: "charges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "period",
                schema: "finance",
                table: "charges",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_charges_tenant_id_contract_id_period",
                schema: "finance",
                table: "charges",
                columns: new[] { "tenant_id", "contract_id", "period" },
                unique: true,
                filter: "contract_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_charges_tenant_id_contract_id_period",
                schema: "finance",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "last_reminded_at",
                schema: "finance",
                table: "open_items");

            migrationBuilder.DropColumn(
                name: "reminders_sent",
                schema: "finance",
                table: "open_items");

            migrationBuilder.DropColumn(
                name: "contract_id",
                schema: "finance",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "period",
                schema: "finance",
                table: "charges");
        }
    }
}
