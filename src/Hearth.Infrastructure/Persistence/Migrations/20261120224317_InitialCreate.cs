using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hearth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    initial_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    initial_balance_date = table.Column<DateOnly>(type: "date", nullable: false),
                    closing_day = table.Column<int>(type: "integer", nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, computedColumnSql: "lower(name)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expected_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incomes", x => x.id);
                    table.ForeignKey(
                        name: "fk_incomes_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closing_month = table.Column<int>(type: "integer", nullable: false),
                    closing_day = table.Column<int>(type: "integer", nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false),
                    charged_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_invoices_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "category_budgets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_month = table.Column<int>(type: "integer", nullable: false),
                    end_month = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_category_budgets", x => x.id);
                    table.ForeignKey(
                        name: "fk_category_budgets_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_category_budgets_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_card_purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    installment_count = table.Column<int>(type: "integer", nullable: false),
                    initial_installment = table.Column<int>(type: "integer", nullable: false),
                    closing_day = table.Column<int>(type: "integer", nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false),
                    condition_kind = table.Column<int>(type: "integer", nullable: false),
                    condition_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_card_purchases", x => x.id);
                    table.ForeignKey(
                        name: "fk_credit_card_purchases_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_credit_card_purchases_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "direct_expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_direct_expenses", x => x.id);
                    table.ForeignKey(
                        name: "fk_direct_expenses_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_direct_expenses_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fixed_expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_month = table.Column<int>(type: "integer", nullable: false),
                    end_month = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fixed_expenses", x => x.id);
                    table.ForeignKey(
                        name: "fk_fixed_expenses_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fixed_expenses_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "category_budget_amount_changes",
                columns: table => new
                {
                    effective_from = table.Column<int>(type: "integer", nullable: false),
                    category_budget_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_category_budget_amount_changes", x => new { x.category_budget_id, x.effective_from });
                    table.ForeignKey(
                        name: "fk_category_budget_amount_changes_category_budgets_category_budget_id",
                        column: x => x.category_budget_id,
                        principalTable: "category_budgets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixed_expense_amount_changes",
                columns: table => new
                {
                    effective_from = table.Column<int>(type: "integer", nullable: false),
                    fixed_expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fixed_expense_amount_changes", x => new { x.fixed_expense_id, x.effective_from });
                    table.ForeignKey(
                        name: "fk_fixed_expense_amount_changes_fixed_expenses_fixed_expense_id",
                        column: x => x.fixed_expense_id,
                        principalTable: "fixed_expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixed_expense_payments",
                columns: table => new
                {
                    month = table.Column<int>(type: "integer", nullable: false),
                    fixed_expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fixed_expense_payments", x => new { x.fixed_expense_id, x.month });
                    table.ForeignKey(
                        name: "fk_fixed_expense_payments_fixed_expenses_fixed_expense_id",
                        column: x => x.fixed_expense_id,
                        principalTable: "fixed_expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_account_id",
                table: "categories",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ux_categories_account_id_normalized_name",
                table: "categories",
                columns: new[] { "account_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_category_budgets_account_id_category_id",
                table: "category_budgets",
                columns: new[] { "account_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_category_budgets_category_id",
                table: "category_budgets",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_card_purchases_account_id_date",
                table: "credit_card_purchases",
                columns: new[] { "account_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_credit_card_purchases_category_id",
                table: "credit_card_purchases",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_direct_expenses_account_id_date",
                table: "direct_expenses",
                columns: new[] { "account_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_direct_expenses_category_id",
                table: "direct_expenses",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_fixed_expenses_account_id",
                table: "fixed_expenses",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_fixed_expenses_category_id",
                table: "fixed_expenses",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_incomes_account_id_expected_date",
                table: "incomes",
                columns: new[] { "account_id", "expected_date" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_account_id",
                table: "invoices",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ux_invoices_account_id_closing_month",
                table: "invoices",
                columns: new[] { "account_id", "closing_month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_budget_amount_changes");

            migrationBuilder.DropTable(
                name: "credit_card_purchases");

            migrationBuilder.DropTable(
                name: "direct_expenses");

            migrationBuilder.DropTable(
                name: "fixed_expense_amount_changes");

            migrationBuilder.DropTable(
                name: "fixed_expense_payments");

            migrationBuilder.DropTable(
                name: "incomes");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "category_budgets");

            migrationBuilder.DropTable(
                name: "fixed_expenses");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
