using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akiron.Modules.Reference.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reference");

            migrationBuilder.CreateTable(
                name: "audit_changes",
                schema: "reference",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_changes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_counters",
                schema: "reference",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    series = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_counters", x => new { x.tenant_id, x.series, x.year });
                });

            migrationBuilder.CreateTable(
                name: "exchange_rates",
                schema: "reference",
                columns: table => new
                {
                    bulletin_date = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    forex_buying = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    forex_selling = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    banknote_buying = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    banknote_selling = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exchange_rates", x => new { x.bulletin_date, x.currency_code });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "reference",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_changes_tenant_id_entity_type_entity_id_occurred_at",
                schema: "reference",
                table: "audit_changes",
                columns: new[] { "tenant_id", "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exchange_rates_currency_code_bulletin_date",
                schema: "reference",
                table: "exchange_rates",
                columns: new[] { "currency_code", "bulletin_date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "reference",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL AND failed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_changes",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "document_counters",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "exchange_rates",
                schema: "reference");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "reference");
        }
    }
}
