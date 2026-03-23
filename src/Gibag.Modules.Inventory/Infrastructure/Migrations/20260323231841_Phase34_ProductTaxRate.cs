using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gibag.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase34_ProductTaxRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "Products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "Products");
        }
    }
}
