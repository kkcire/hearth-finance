using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class FixedIncomeConfiguration : IEntityTypeConfiguration<FixedIncome>
{
    public void Configure(EntityTypeBuilder<FixedIncome> builder)
    {
        builder.ToTable("fixed_incomes");

        builder.HasKey(income => income.Id)
            .HasName("pk_fixed_incomes");

        builder.Property(income => income.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(income => income.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(income => income.Description)
            .HasColumnName("description")
            .HasMaxLength(DomainValidation.MaxDescriptionLength)
            .IsRequired();

        builder.Property(income => income.ConditionId)
            .HasColumnName("condition_id")
            .HasDefaultValue(SystemPurchaseConditions.OwnId)
            .IsRequired();

        builder.Property(income => income.ExpectedDayOfMonth)
            .HasColumnName("expected_day_of_month")
            .IsRequired();

        builder.Property(income => income.StartMonth)
            .HasColumnName("start_month")
            .IsRequired();

        builder.Property(income => income.EndMonth)
            .HasColumnName("end_month");

        builder.Ignore(income => income.IsOwn);
        builder.Ignore(income => income.AmountHistory);
        builder.Ignore(income => income.Receipts);

        builder.OwnsMany<FixedIncomeAmountChange>("_amountHistory", history =>
        {
            history.ToTable("fixed_income_amount_changes");

            history.WithOwner()
                .HasForeignKey("fixed_income_id")
                .HasConstraintName("fk_fixed_income_amount_changes_fixed_incomes_fixed_income_id");

            history.Property<Guid>("fixed_income_id")
                .HasColumnName("fixed_income_id");

            history.Property(change => change.EffectiveFrom)
                .HasColumnName("effective_from")
                .IsRequired();

            history.Property(change => change.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,2)")
                .IsRequired();

            history.HasKey("fixed_income_id", nameof(FixedIncomeAmountChange.EffectiveFrom))
                .HasName("pk_fixed_income_amount_changes");
        });

        builder.OwnsMany<FixedIncomeReceipt>("_receipts", receipts =>
        {
            receipts.ToTable("fixed_income_receipts");

            receipts.WithOwner()
                .HasForeignKey("fixed_income_id")
                .HasConstraintName("fk_fixed_income_receipts_fixed_incomes_fixed_income_id");

            receipts.Property<Guid>("fixed_income_id")
                .HasColumnName("fixed_income_id");

            receipts.Property(receipt => receipt.Month)
                .HasColumnName("month")
                .IsRequired();

            receipts.Property(receipt => receipt.ReceivedOn)
                .HasColumnName("received_on")
                .IsRequired();

            receipts.Property(receipt => receipt.ActualAmount)
                .HasColumnName("actual_amount")
                .HasColumnType("numeric(18,2)")
                .IsRequired();

            receipts.HasKey("fixed_income_id", nameof(FixedIncomeReceipt.Month))
                .HasName("pk_fixed_income_receipts");
        });

        // Prevent dual-OwnsMany Cartesian explosion; FixedIncomeRepository loads both explicitly via AsSplitQuery().
        builder.Navigation("_amountHistory").AutoInclude(false);
        builder.Navigation("_receipts").AutoInclude(false);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(income => income.AccountId)
            .HasConstraintName("fk_fixed_incomes_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PurchaseCondition>()
            .WithMany()
            .HasForeignKey(income => income.ConditionId)
            .HasConstraintName("fk_fixed_incomes_purchase_conditions_condition_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(income => income.AccountId)
            .HasDatabaseName("ix_fixed_incomes_account_id");

        builder.HasIndex(income => income.ConditionId)
            .HasDatabaseName("ix_fixed_incomes_condition_id");
    }
}
