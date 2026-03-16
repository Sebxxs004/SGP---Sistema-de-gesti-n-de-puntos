using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gibag.Modules.Sales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase10_CashSessionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartedAt",
                table: "CashRegisterSessions",
                newName: "OpenedAt");

            migrationBuilder.RenameColumn(
                name: "InitialAmount",
                table: "CashRegisterSessions",
                newName: "InitialBalance");

            migrationBuilder.RenameColumn(
                name: "FinalAmount",
                table: "CashRegisterSessions",
                newName: "FinalBalanceExpected");

            migrationBuilder.RenameColumn(
                name: "EndedAt",
                table: "CashRegisterSessions",
                newName: "ClosedAt");

            migrationBuilder.AddColumn<decimal>(
                name: "FinalBalanceEncounted",
                table: "CashRegisterSessions",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalBalanceEncounted",
                table: "CashRegisterSessions");

            migrationBuilder.RenameColumn(
                name: "OpenedAt",
                table: "CashRegisterSessions",
                newName: "StartedAt");

            migrationBuilder.RenameColumn(
                name: "InitialBalance",
                table: "CashRegisterSessions",
                newName: "InitialAmount");

            migrationBuilder.RenameColumn(
                name: "FinalBalanceExpected",
                table: "CashRegisterSessions",
                newName: "FinalAmount");

            migrationBuilder.RenameColumn(
                name: "ClosedAt",
                table: "CashRegisterSessions",
                newName: "EndedAt");
        }
    }
}
