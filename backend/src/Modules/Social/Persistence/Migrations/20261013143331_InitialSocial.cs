using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akiron.Modules.Social.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSocial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "social");

            migrationBuilder.CreateTable(
                name: "audit_changes",
                schema: "social",
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
                name: "content_posts",
                schema: "social",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    caption = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    hashtags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    platform_codes = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    client_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decided_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent_to_client_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    publish_reminder_sent_for = table.Column<DateOnly>(type: "date", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_posts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_counters",
                schema: "social",
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
                schema: "social",
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
                schema: "social",
                table: "audit_changes",
                columns: new[] { "tenant_id", "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_content_posts_status_planned_date",
                schema: "social",
                table: "content_posts",
                columns: new[] { "status", "planned_date" });

            migrationBuilder.CreateIndex(
                name: "ix_content_posts_tenant_id_party_id_planned_date",
                schema: "social",
                table: "content_posts",
                columns: new[] { "tenant_id", "party_id", "planned_date" });

            migrationBuilder.CreateIndex(
                name: "ix_content_posts_tenant_id_planned_date",
                schema: "social",
                table: "content_posts",
                columns: new[] { "tenant_id", "planned_date" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "social",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL AND failed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_changes",
                schema: "social");

            migrationBuilder.DropTable(
                name: "content_posts",
                schema: "social");

            migrationBuilder.DropTable(
                name: "document_counters",
                schema: "social");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "social");
        }
    }
}
