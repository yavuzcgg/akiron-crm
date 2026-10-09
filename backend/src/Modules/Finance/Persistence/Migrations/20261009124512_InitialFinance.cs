using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Akiron.Modules.Finance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "audit_changes",
                schema: "finance",
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
                name: "charges",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_counters",
                schema: "finance",
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
                name: "ledger_entries",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    reverses_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "finance",
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

            migrationBuilder.CreateTable(
                name: "receipts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    account = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "open_items",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    allocated = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_open = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_open_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_open_items_charges_charge_id",
                        column: x => x.charge_id,
                        principalSchema: "finance",
                        principalTable: "charges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "allocations",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    open_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_reversed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allocations", x => x.id);
                    table.ForeignKey(
                        name: "fk_allocations_open_items_open_item_id",
                        column: x => x.open_item_id,
                        principalSchema: "finance",
                        principalTable: "open_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_allocations_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalSchema: "finance",
                        principalTable: "receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_allocations_open_item_id",
                schema: "finance",
                table: "allocations",
                column: "open_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_receipt_id",
                schema: "finance",
                table: "allocations",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_tenant_id_open_item_id",
                schema: "finance",
                table: "allocations",
                columns: new[] { "tenant_id", "open_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_changes_tenant_id_entity_type_entity_id_occurred_at",
                schema: "finance",
                table: "audit_changes",
                columns: new[] { "tenant_id", "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_charges_tenant_id_number",
                schema: "finance",
                table: "charges",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_charges_tenant_id_party_id_date",
                schema: "finance",
                table: "charges",
                columns: new[] { "tenant_id", "party_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_charges_tenant_id_quote_id",
                schema: "finance",
                table: "charges",
                columns: new[] { "tenant_id", "quote_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_tenant_id_party_id_currency_date_sequence",
                schema: "finance",
                table: "ledger_entries",
                columns: new[] { "tenant_id", "party_id", "currency", "date", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_tenant_id_source_source_id",
                schema: "finance",
                table: "ledger_entries",
                columns: new[] { "tenant_id", "source", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_open_items_charge_id",
                schema: "finance",
                table: "open_items",
                column: "charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_open_items_tenant_id_party_id_is_open_due_date",
                schema: "finance",
                table: "open_items",
                columns: new[] { "tenant_id", "party_id", "is_open", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "finance",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_tenant_id_number",
                schema: "finance",
                table: "receipts",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipts_tenant_id_party_id_date",
                schema: "finance",
                table: "receipts",
                columns: new[] { "tenant_id", "party_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "audit_changes",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "document_counters",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "open_items",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "receipts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "charges",
                schema: "finance");
        }
    }
}
