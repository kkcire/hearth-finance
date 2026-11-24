using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hearth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_conditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, computedColumnSql: "lower(name)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_conditions", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_conditions_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "purchase_conditions",
                columns: new[] { "id", "is_archived", "name", "owner_user_id" },
                values: new object[,]
                {
                    { new Guid("01900000-0000-7000-8000-000000000201"), false, "Própria", null },
                    { new Guid("01900000-0000-7000-8000-000000000202"), false, "Outros", null }
                });

            migrationBuilder.AddColumn<Guid>(
                name: "condition_id",
                table: "credit_card_purchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE credit_card_purchases
                SET condition_id = CASE
                    WHEN condition_kind = 1 THEN '01900000-0000-7000-8000-000000000202'::uuid
                    ELSE '01900000-0000-7000-8000-000000000201'::uuid
                END
                WHERE condition_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "condition_id",
                table: "credit_card_purchases",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "condition_kind",
                table: "credit_card_purchases");

            migrationBuilder.DropColumn(
                name: "condition_name",
                table: "credit_card_purchases");

            migrationBuilder.CreateIndex(
                name: "ix_credit_card_purchases_condition_id",
                table: "credit_card_purchases",
                column: "condition_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_conditions_owner_user_id",
                table: "purchase_conditions",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_purchase_conditions_owner_normalized_name",
                table: "purchase_conditions",
                columns: new[] { "owner_user_id", "normalized_name" },
                unique: true,
                filter: "owner_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_purchase_conditions_system_normalized_name",
                table: "purchase_conditions",
                column: "normalized_name",
                unique: true,
                filter: "owner_user_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_credit_card_purchases_purchase_conditions_condition_id",
                table: "credit_card_purchases",
                column: "condition_id",
                principalTable: "purchase_conditions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_credit_card_purchases_purchase_conditions_condition_id",
                table: "credit_card_purchases");

            migrationBuilder.DropTable(
                name: "purchase_conditions");

            migrationBuilder.DropIndex(
                name: "ix_credit_card_purchases_condition_id",
                table: "credit_card_purchases");

            migrationBuilder.DropColumn(
                name: "condition_id",
                table: "credit_card_purchases");

            migrationBuilder.AddColumn<int>(
                name: "condition_kind",
                table: "credit_card_purchases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "condition_name",
                table: "credit_card_purchases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
