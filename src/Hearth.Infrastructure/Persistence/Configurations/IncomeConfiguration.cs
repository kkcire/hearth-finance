using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.ToTable("incomes");

        builder.HasKey(income => income.Id)
            .HasName("pk_incomes");

        builder.Property(income => income.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(income => income.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(income => income.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(income => income.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(income => income.Description)
            .HasColumnName("description")
            .HasMaxLength(DomainValidation.MaxDescriptionLength)
            .IsRequired();

        builder.Property(income => income.ConditionId)
            .HasColumnName("condition_id")
            .HasDefaultValue(SystemPurchaseConditions.OwnId)
            .IsRequired();

        builder.Ignore(income => income.IsOwn);
        builder.Ignore(income => income.Month);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(income => income.AccountId)
            .HasConstraintName("fk_incomes_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PurchaseCondition>()
            .WithMany()
            .HasForeignKey(income => income.ConditionId)
            .HasConstraintName("fk_incomes_purchase_conditions_condition_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(income => new { income.AccountId, income.Date })
            .HasDatabaseName("ix_incomes_account_id_date");

        builder.HasIndex(income => income.ConditionId)
            .HasDatabaseName("ix_incomes_condition_id");
    }
}
