using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class CreditCardPurchaseConfiguration : IEntityTypeConfiguration<CreditCardPurchase>
{
    public void Configure(EntityTypeBuilder<CreditCardPurchase> builder)
    {
        builder.ToTable("credit_card_purchases");

        builder.HasKey(purchase => purchase.Id)
            .HasName("pk_credit_card_purchases");

        builder.Property(purchase => purchase.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(purchase => purchase.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(purchase => purchase.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.Property(purchase => purchase.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(purchase => purchase.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(purchase => purchase.InstallmentCount)
            .HasColumnName("installment_count")
            .IsRequired();

        builder.Property(purchase => purchase.InitialInstallment)
            .HasColumnName("initial_installment")
            .IsRequired();

        builder.Property(purchase => purchase.Description)
            .HasColumnName("description")
            .HasMaxLength(DomainValidation.MaxDescriptionLength)
            .IsRequired();

        builder.OwnsOne(purchase => purchase.Rule, rule =>
        {
            rule.Property(r => r.ClosingDay)
                .HasColumnName("closing_day")
                .IsRequired();

            rule.Property(r => r.DueDay)
                .HasColumnName("due_day")
                .IsRequired();
        });

        builder.Navigation(purchase => purchase.Rule).IsRequired();

        builder.OwnsOne(purchase => purchase.Condition, condition =>
        {
            condition.Property(c => c.Kind)
                .HasColumnName("condition_kind")
                .IsRequired();

            condition.Property(c => c.Name)
                .HasColumnName("condition_name")
                .HasMaxLength(DomainValidation.MaxNameLength);
        });

        builder.Navigation(purchase => purchase.Condition).IsRequired();

        builder.Ignore(purchase => purchase.FirstCycle);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(purchase => purchase.AccountId)
            .HasConstraintName("fk_credit_card_purchases_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(purchase => purchase.CategoryId)
            .HasConstraintName("fk_credit_card_purchases_categories_category_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(purchase => new { purchase.AccountId, purchase.Date })
            .HasDatabaseName("ix_credit_card_purchases_account_id_date");

        builder.HasIndex(purchase => purchase.CategoryId)
            .HasDatabaseName("ix_credit_card_purchases_category_id");
    }
}
