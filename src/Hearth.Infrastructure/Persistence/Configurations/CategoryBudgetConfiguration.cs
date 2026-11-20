using Hearth.Domain.Entities;
using Hearth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class CategoryBudgetConfiguration : IEntityTypeConfiguration<CategoryBudget>
{
    public void Configure(EntityTypeBuilder<CategoryBudget> builder)
    {
        builder.ToTable("category_budgets");

        builder.HasKey(budget => budget.Id)
            .HasName("pk_category_budgets");

        builder.Property(budget => budget.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(budget => budget.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(budget => budget.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.Property(budget => budget.StartMonth)
            .HasColumnName("start_month")
            .IsRequired();

        builder.Property(budget => budget.EndMonth)
            .HasColumnName("end_month");

        builder.Ignore(budget => budget.AmountHistory);

        builder.OwnsMany<AmountChange>("_amountHistory", history =>
        {
            history.ToTable("category_budget_amount_changes");

            history.WithOwner()
                .HasForeignKey("category_budget_id")
                .HasConstraintName("fk_category_budget_amount_changes_category_budgets_category_budget_id");

            history.Property<Guid>("category_budget_id")
                .HasColumnName("category_budget_id");

            history.Property(change => change.EffectiveFrom)
                .HasColumnName("effective_from")
                .IsRequired();

            history.Property(change => change.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,2)")
                .IsRequired();

            history.HasKey("category_budget_id", nameof(AmountChange.EffectiveFrom))
                .HasName("pk_category_budget_amount_changes");
        });

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(budget => budget.AccountId)
            .HasConstraintName("fk_category_budgets_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(budget => budget.CategoryId)
            .HasConstraintName("fk_category_budgets_categories_category_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(budget => new { budget.AccountId, budget.CategoryId })
            .HasDatabaseName("ix_category_budgets_account_id_category_id");

        builder.HasIndex(budget => budget.CategoryId)
            .HasDatabaseName("ix_category_budgets_category_id");
    }
}
