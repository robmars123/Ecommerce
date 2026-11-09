using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectionsAndSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_dashboard_orders",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    currency = table.Column<string>(type: "character(3)", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_dashboard_orders", x => x.order_id);
                });

            migrationBuilder.CreateTable(
                name: "admin_dashboard_refunds",
                columns: table => new
                {
                    refund_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    succeeded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_dashboard_refunds", x => x.refund_id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_sales_daily",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_date = table.Column<DateOnly>(type: "date", nullable: false),
                    units = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_product_sales_daily", x => new { x.product_id, x.sales_date });
                });

            migrationBuilder.CreateTable(
                name: "consumer_checkpoints",
                columns: table => new
                {
                    consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_sequence = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumer_checkpoints", x => x.consumer);
                });

            migrationBuilder.CreateTable(
                name: "fulfillment_sagas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fulfillment_sagas", x => x.id);
                    table.CheckConstraint("ck_fulfillment_sagas_status", "status IN ('Compensating','Compensated')");
                    table.ForeignKey(
                        name: "fk_fulfillment_sagas_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projection_versions",
                columns: table => new
                {
                    projector = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projection_versions", x => new { x.projector, x.aggregate_type, x.aggregate_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_dashboard_orders_paid_at",
                table: "admin_dashboard_orders",
                column: "paid_at");

            migrationBuilder.CreateIndex(
                name: "ix_admin_dashboard_orders_status",
                table: "admin_dashboard_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_admin_dashboard_refunds_succeeded_at",
                table: "admin_dashboard_refunds",
                column: "succeeded_at");

            migrationBuilder.CreateIndex(
                name: "ux_fulfillment_sagas_order_id",
                table: "fulfillment_sagas",
                column: "order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_dashboard_orders");

            migrationBuilder.DropTable(
                name: "admin_dashboard_refunds");

            migrationBuilder.DropTable(
                name: "catalog_product_sales_daily");

            migrationBuilder.DropTable(
                name: "consumer_checkpoints");

            migrationBuilder.DropTable(
                name: "fulfillment_sagas");

            migrationBuilder.DropTable(
                name: "projection_versions");
        }
    }
}
