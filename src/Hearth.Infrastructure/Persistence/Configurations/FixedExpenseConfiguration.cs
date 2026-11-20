using Hearth.Domain;
using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class FixedExpenseConfiguration : IEntityTypeConfiguration<FixedExpense>
{
    public void Configure(EntityTypeBuilder<FixedExpense> builder)
    {
        builder.ToTable("fixed_expenses");

        builder.HasKey(expense => expense.Id)
            .HasName("pk_fixed_expenses");

        builder.Property(expense => expense.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(expense => expense.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(expense => expense.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.Property(expense => expense.StartMonth)
            .HasColumnName("start_month")
            .IsRequired();

        builder.Property(expense => expense.EndMonth)
            .HasColumnName("end_month");

        builder.Property(expense => expense.Description)
            .HasColumnName("description")
            .HasMaxLength(DomainValidation.MaxDescriptionLength)
            .IsRequired();

        builder.Ignore(expense => expense.AmountHistory);
        builder.Ignore(expense => expense.Payments);

        builder.OwnsMany<AmountChange>("_amountHistory", history =>
        {
            history.ToTable("fixed_expense_amount_changes");

            history.WithOwner()
                .HasForeignKey("fixed_expense_id")
                .HasConstraintName("fk_fixed_expense_amount_changes_fixed_expenses_fixed_expense_id");

            history.Property<Guid>("fixed_expense_id")
                .HasColumnName("fixed_expense_id");

            history.Property(change => change.EffectiveFrom)
                .HasColumnName("effective_from")
                .IsRequired();

            history.Property(change => change.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,2)")
                .IsRequired();

            history.HasKey("fixed_expense_id", nameof(AmountChange.EffectiveFrom))
                .HasName("pk_fixed_expense_amount_changes");
        });

        builder.OwnsMany<FixedExpensePayment>("_payments", payments =>
        {
            payments.ToTable("fixed_expense_payments");

            payments.WithOwner()
                .HasForeignKey("fixed_expense_id")
                .HasConstraintName("fk_fixed_expense_payments_fixed_expenses_fixed_expense_id");

            payments.Property<Guid>("fixed_expense_id")
                .HasColumnName("fixed_expense_id");

            payments.Property(payment => payment.Month)
                .HasColumnName("month")
                .IsRequired();

            payments.Property(payment => payment.PaidOn)
                .HasColumnName("paid_on")
                .IsRequired();

            payments.HasKey("fixed_expense_id", nameof(FixedExpensePayment.Month))
                .HasName("pk_fixed_expense_payments");
        });

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(expense => expense.AccountId)
            .HasConstraintName("fk_fixed_expenses_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(expense => expense.CategoryId)
            .HasConstraintName("fk_fixed_expenses_categories_category_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(expense => expense.AccountId)
            .HasDatabaseName("ix_fixed_expenses_account_id");

        builder.HasIndex(expense => expense.CategoryId)
            .HasDatabaseName("ix_fixed_expenses_category_id");
    }
}
