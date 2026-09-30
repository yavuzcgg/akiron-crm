using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akiron.Modules.Jobs.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jobs");

            migrationBuilder.CreateTable(
                name: "audit_changes",
                schema: "jobs",
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
                schema: "jobs",
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
                name: "outbox_messages",
                schema: "jobs",
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
                name: "stages",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    party_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<double>(type: "double precision", nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_orders_stages_stage_id",
                        column: x => x.stage_id,
                        principalSchema: "jobs",
                        principalTable: "stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "time_entries",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    minutes = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_billable = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_entries_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalSchema: "jobs",
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_order_assignees",
                schema: "jobs",
                columns: table => new
                {
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_assignees", x => new { x.work_order_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_work_order_assignees_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalSchema: "jobs",
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_tasks",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    done_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_order_tasks_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalSchema: "jobs",
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_changes_tenant_id_entity_type_entity_id_occurred_at",
                schema: "jobs",
                table: "audit_changes",
                columns: new[] { "tenant_id", "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "jobs",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stages_tenant_id_position",
                schema: "jobs",
                table: "stages",
                columns: new[] { "tenant_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_one_running_per_user",
                schema: "jobs",
                table: "time_entries",
                columns: new[] { "tenant_id", "user_id" },
                unique: true,
                filter: "ended_at IS NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_tenant_id_user_id_started_at",
                schema: "jobs",
                table: "time_entries",
                columns: new[] { "tenant_id", "user_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_tenant_id_work_order_id",
                schema: "jobs",
                table: "time_entries",
                columns: new[] { "tenant_id", "work_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_work_order_id",
                schema: "jobs",
                table: "time_entries",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_assignees_tenant_id_user_id",
                schema: "jobs",
                table: "work_order_assignees",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_order_tasks_tenant_id_work_order_id_position",
                schema: "jobs",
                table: "work_order_tasks",
                columns: new[] { "tenant_id", "work_order_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_work_order_tasks_work_order_id",
                schema: "jobs",
                table: "work_order_tasks",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_stage_id",
                schema: "jobs",
                table: "work_orders",
                column: "stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_id_number",
                schema: "jobs",
                table: "work_orders",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_id_party_id",
                schema: "jobs",
                table: "work_orders",
                columns: new[] { "tenant_id", "party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_id_stage_id_rank",
                schema: "jobs",
                table: "work_orders",
                columns: new[] { "tenant_id", "stage_id", "rank" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_changes",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "document_counters",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "time_entries",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "work_order_assignees",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "work_order_tasks",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "work_orders",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "stages",
                schema: "jobs");
        }
    }
}
