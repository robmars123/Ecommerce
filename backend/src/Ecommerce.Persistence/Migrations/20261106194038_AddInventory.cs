using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // inventory_stock.Version maps to the xmin system column (same as products.Version): no DDL is needed.

            migrationBuilder.CreateTable(
                name: "inventory_stock",
                columns: table => new
                {
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_on_hand = table.Column<int>(type: "integer", nullable: false),
                    quantity_reserved = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_stock", x => x.product_variant_id);
                    table.CheckConstraint("ck_inventory_stock_quantities", "quantity_on_hand - quantity_reserved >= 0 AND quantity_reserved >= 0");
                });

            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_reservations", x => x.id);
                    table.CheckConstraint("ck_inventory_reservations_quantity", "quantity > 0");
                    table.CheckConstraint("ck_inventory_reservations_status", "status IN ('Active','Committed','Released','Expired')");
                    table.ForeignKey(
                        name: "fk_inventory_reservations_stock",
                        column: x => x.product_variant_id,
                        principalTable: "inventory_stock",
                        principalColumn: "product_variant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_reservations_expires_at_active",
                table: "inventory_reservations",
                column: "expires_at",
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_reservations_product_variant_id",
                table: "inventory_reservations",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "ux_inventory_reservations_order_variant_active",
                table: "inventory_reservations",
                columns: new[] { "order_id", "product_variant_id" },
                unique: true,
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_reservations");

            migrationBuilder.DropTable(
                name: "inventory_stock");
        }
    }
}
