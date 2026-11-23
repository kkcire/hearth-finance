using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Hearth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignMvpSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_categories_accounts_account_id",
                table: "categories");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropIndex(
                name: "ix_categories_account_id",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "ux_categories_account_id_normalized_name",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "account_id",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "closing_day",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "due_day",
                table: "accounts");

            migrationBuilder.AddColumn<Guid>(
                name: "credit_card_id",
                table: "credit_card_purchases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_initial_balance_confirmed",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "credit_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    closing_day = table.Column<int>(type: "integer", nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false),
                    limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_cards", x => x.id);
                    table.ForeignKey(
                        name: "fk_credit_cards_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "is_archived", "name", "owner_user_id" },
                values: new object[,]
                {
                    { new Guid("01900000-0000-7000-8000-000000000101"), false, "Transporte", null },
                    { new Guid("01900000-0000-7000-8000-000000000102"), false, "Alimentação", null },
                    { new Guid("01900000-0000-7000-8000-000000000103"), false, "Saúde", null },
                    { new Guid("01900000-0000-7000-8000-000000000104"), false, "Educação", null },
                    { new Guid("01900000-0000-7000-8000-000000000105"), false, "Entretenimento", null },
                    { new Guid("01900000-0000-7000-8000-000000000106"), false, "Serviços", null },
                    { new Guid("01900000-0000-7000-8000-000000000107"), false, "Compras", null },
                    { new Guid("01900000-0000-7000-8000-000000000108"), false, "Pessoal", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_credit_card_purchases_credit_card_id",
                table: "credit_card_purchases",
                column: "credit_card_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_owner_user_id",
                table: "categories",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_categories_owner_normalized_name",
                table: "categories",
                columns: new[] { "owner_user_id", "normalized_name" },
                unique: true,
                filter: "owner_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_categories_system_normalized_name",
                table: "categories",
                column: "normalized_name",
                unique: true,
                filter: "owner_user_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_credit_cards_account_id_active",
                table: "credit_cards",
                column: "account_id",
                unique: true,
                filter: "is_archived = false");

            migrationBuilder.AddForeignKey(
                name: "fk_categories_users_owner_user_id",
                table: "categories",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_credit_card_purchases_credit_cards_credit_card_id",
                table: "credit_card_purchases",
                column: "credit_card_id",
                principalTable: "credit_cards",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_categories_users_owner_user_id",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "fk_credit_card_purchases_credit_cards_credit_card_id",
                table: "credit_card_purchases");

            migrationBuilder.DropTable(
                name: "credit_cards");

            migrationBuilder.DropIndex(
                name: "ix_credit_card_purchases_credit_card_id",
                table: "credit_card_purchases");

            migrationBuilder.DropIndex(
                name: "ix_categories_owner_user_id",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "ux_categories_owner_normalized_name",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "ux_categories_system_normalized_name",
                table: "categories");

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8000-000000000101"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8000-000000000102"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8000-000000000103"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8000-000000000104"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8000-000000000105"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8000-000000000106"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8000-000000000107"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8000-000000000108"));

            migrationBuilder.DropColumn(
                name: "credit_card_id",
                table: "credit_card_purchases");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "is_initial_balance_confirmed",
                table: "accounts");

            migrationBuilder.AddColumn<Guid>(
                name: "account_id",
                table: "categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "closing_day",
                table: "accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "due_day",
                table: "accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    charged_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    closing_month = table.Column<int>(type: "integer", nullable: false),
                    closing_day = table.Column<int>(type: "integer", nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false)
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
                name: "ix_invoices_account_id",
                table: "invoices",
                column: "account_id");

            migrationBuilder.AddForeignKey(
                name: "fk_categories_accounts_account_id",
                table: "categories",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
