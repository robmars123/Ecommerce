using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRuntimeAndReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processed_events",
                columns: table => new
                {
                    consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_events", x => new { x.consumer, x.event_id });
                    table.CheckConstraint("ck_processed_events_status", "status IN ('Retrying','Done','DeadLetter')");
                });

            migrationBuilder.CreateTable(
                name: "return_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    admin_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_return_requests", x => x.id);
                    table.CheckConstraint("ck_return_requests_status", "status IN ('Requested','Approved','Declined','Refunded')");
                    table.ForeignKey(
                        name: "fk_return_requests_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_processed_events_next_attempt_at",
                table: "processed_events",
                column: "next_attempt_at",
                filter: "status <> 'Done'");

            migrationBuilder.CreateIndex(
                name: "ix_return_requests_status_created_at",
                table: "return_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_return_requests_open_order_id",
                table: "return_requests",
                column: "order_id",
                unique: true,
                filter: "status IN ('Requested','Approved')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_events");

            migrationBuilder.DropTable(
                name: "return_requests");
        }
    }
}
