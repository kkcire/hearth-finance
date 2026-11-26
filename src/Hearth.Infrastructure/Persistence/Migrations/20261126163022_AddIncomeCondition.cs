using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hearth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "condition_id",
                table: "incomes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("01900000-0000-7000-8000-000000000201"));

            migrationBuilder.CreateIndex(
                name: "ix_incomes_condition_id",
                table: "incomes",
                column: "condition_id");

            migrationBuilder.AddForeignKey(
                name: "fk_incomes_purchase_conditions_condition_id",
                table: "incomes",
                column: "condition_id",
                principalTable: "purchase_conditions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_incomes_purchase_conditions_condition_id",
                table: "incomes");

            migrationBuilder.DropIndex(
                name: "ix_incomes_condition_id",
                table: "incomes");

            migrationBuilder.DropColumn(
                name: "condition_id",
                table: "incomes");
        }
    }
}
