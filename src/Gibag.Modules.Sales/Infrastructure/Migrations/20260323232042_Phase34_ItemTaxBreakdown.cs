using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gibag.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase34_ItemTaxBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "SaleDetails",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "SaleDetails",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "SaleDetails");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "SaleDetails");
        }
    }
}
