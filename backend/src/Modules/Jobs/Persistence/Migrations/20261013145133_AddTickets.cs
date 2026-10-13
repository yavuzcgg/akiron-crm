using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akiron.Modules.Jobs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tickets",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    from_client = table.Column<bool>(type: "boolean", nullable: false),
                    requester_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    response_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_response_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    overdue_notified = table.Column<bool>(type: "boolean", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tickets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_first_response_at_response_due_at",
                schema: "jobs",
                table: "tickets",
                columns: new[] { "first_response_at", "response_due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_number",
                schema: "jobs",
                table: "tickets",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_party_id",
                schema: "jobs",
                table: "tickets",
                columns: new[] { "tenant_id", "party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_status_priority",
                schema: "jobs",
                table: "tickets",
                columns: new[] { "tenant_id", "status", "priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tickets",
                schema: "jobs");
        }
    }
}
