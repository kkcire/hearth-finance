using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hearth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountOwnerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "accounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_accounts_owner_user_id",
                table: "accounts",
                column: "owner_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_accounts_users_owner_user_id",
                table: "accounts",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_accounts_users_owner_user_id",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_owner_user_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "accounts");
        }
    }
}
