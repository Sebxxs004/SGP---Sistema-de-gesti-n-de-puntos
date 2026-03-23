using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gibag.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase29_LowStockAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinStockLevel",
                table: "Products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinStockLevel",
                table: "Products");
        }
    }
}
