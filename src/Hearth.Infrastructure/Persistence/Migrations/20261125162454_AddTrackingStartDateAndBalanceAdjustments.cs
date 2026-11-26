using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hearth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingStartDateAndBalanceAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "initial_balance_date",
                table: "accounts",
                newName: "tracking_start_date");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Backfill existing accounts non-destructively:
            // 1) Derive created_at in UTC from the previous initial_balance_date evaluated in America/Sao_Paulo.
            // 2) Clamp tracking_start_date to Day 1 of the earliest date between created_at (BRT),
            //    the previous reference date, and the account's oldest existing transaction so no data is orphaned.
            migrationBuilder.Sql(
                """
                UPDATE accounts
                SET created_at = (tracking_start_date::timestamp AT TIME ZONE 'America/Sao_Paulo')
                WHERE created_at <= TIMESTAMPTZ '0001-01-02 00:00:00+00';

                UPDATE accounts AS a
                SET tracking_start_date = date_trunc(
                    'month',
                    LEAST(
                        (a.created_at AT TIME ZONE 'America/Sao_Paulo')::date,
                        a.tracking_start_date,
                        (SELECT MIN(de.date) FROM direct_expenses AS de WHERE de.account_id = a.id),
                        (SELECT MIN(LEAST(i.expected_date, i.received_on)) FROM incomes AS i WHERE i.account_id = a.id),
                        (SELECT make_date(MIN(fe.start_month) / 100, MIN(fe.start_month) % 100, 1) FROM fixed_expenses AS fe WHERE fe.account_id = a.id),
                        (SELECT make_date(MIN(cb.start_month) / 100, MIN(cb.start_month) % 100, 1) FROM category_budgets AS cb WHERE cb.account_id = a.id)
                    )
                )::date;
                """);

            migrationBuilder.CreateTable(
                name: "balance_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_balance_adjustments", x => x.id);
                    table.CheckConstraint("ck_balance_adjustments_amount_nonzero", "amount <> 0");
                    table.ForeignKey(
                        name: "fk_balance_adjustments_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_accounts_tracking_start_day_one",
                table: "accounts",
                sql: "EXTRACT(DAY FROM tracking_start_date) = 1");

            migrationBuilder.CreateIndex(
                name: "ix_balance_adjustments_account_id_date",
                table: "balance_adjustments",
                columns: new[] { "account_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "balance_adjustments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_accounts_tracking_start_day_one",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "accounts");

            migrationBuilder.RenameColumn(
                name: "tracking_start_date",
                table: "accounts",
                newName: "initial_balance_date");
        }
    }
}
