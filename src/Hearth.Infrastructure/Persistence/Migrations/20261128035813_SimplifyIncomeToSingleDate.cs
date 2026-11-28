using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hearth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyIncomeToSingleDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE incomes SET expected_date = COALESCE(received_on, expected_date);");

            migrationBuilder.RenameColumn(
                name: "expected_date",
                table: "incomes",
                newName: "date");

            migrationBuilder.RenameIndex(
                name: "ix_incomes_account_id_expected_date",
                table: "incomes",
                newName: "ix_incomes_account_id_date");

            migrationBuilder.DropColumn(
                name: "received_on",
                table: "incomes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "date",
                table: "incomes",
                newName: "expected_date");

            migrationBuilder.RenameIndex(
                name: "ix_incomes_account_id_date",
                table: "incomes",
                newName: "ix_incomes_account_id_expected_date");

            migrationBuilder.AddColumn<DateOnly>(
                name: "received_on",
                table: "incomes",
                type: "date",
                nullable: true);
        }
    }
}
