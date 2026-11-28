using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hearth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedIncomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fixed_incomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    condition_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("01900000-0000-7000-8000-000000000201")),
                    expected_day_of_month = table.Column<int>(type: "integer", nullable: false),
                    start_month = table.Column<int>(type: "integer", nullable: false),
                    end_month = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fixed_incomes", x => x.id);
                    table.ForeignKey(
                        name: "fk_fixed_incomes_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fixed_incomes_purchase_conditions_condition_id",
                        column: x => x.condition_id,
                        principalTable: "purchase_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fixed_income_amount_changes",
                columns: table => new
                {
                    effective_from = table.Column<int>(type: "integer", nullable: false),
                    fixed_income_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fixed_income_amount_changes", x => new { x.fixed_income_id, x.effective_from });
                    table.ForeignKey(
                        name: "fk_fixed_income_amount_changes_fixed_incomes_fixed_income_id",
                        column: x => x.fixed_income_id,
                        principalTable: "fixed_incomes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixed_income_receipts",
                columns: table => new
                {
                    month = table.Column<int>(type: "integer", nullable: false),
                    fixed_income_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fixed_income_receipts", x => new { x.fixed_income_id, x.month });
                    table.ForeignKey(
                        name: "fk_fixed_income_receipts_fixed_incomes_fixed_income_id",
                        column: x => x.fixed_income_id,
                        principalTable: "fixed_incomes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fixed_incomes_account_id",
                table: "fixed_incomes",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_fixed_incomes_condition_id",
                table: "fixed_incomes",
                column: "condition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fixed_income_amount_changes");

            migrationBuilder.DropTable(
                name: "fixed_income_receipts");

            migrationBuilder.DropTable(
                name: "fixed_incomes");
        }
    }
}
