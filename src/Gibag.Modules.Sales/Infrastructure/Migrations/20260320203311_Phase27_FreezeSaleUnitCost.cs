using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gibag.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase27_FreezeSaleUnitCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "SaleDetails",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "SaleDetails");
        }
    }
}
