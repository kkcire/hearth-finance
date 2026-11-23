using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.ToTable("credit_cards");

        builder.HasKey(card => card.Id)
            .HasName("pk_credit_cards");

        builder.Property(card => card.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(card => card.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(card => card.Name)
            .HasColumnName("name")
            .HasMaxLength(DomainValidation.MaxNameLength);

        builder.Property(card => card.Limit)
            .HasColumnName("limit")
            .HasColumnType("numeric(18,2)");

        builder.Property(card => card.IsArchived)
            .HasColumnName("is_archived")
            .IsRequired();

        builder.OwnsOne(card => card.BillingRule, rule =>
        {
            rule.Property(r => r.ClosingDay)
                .HasColumnName("closing_day")
                .IsRequired();

            rule.Property(r => r.DueDay)
                .HasColumnName("due_day")
                .IsRequired();
        });

        builder.Navigation(card => card.BillingRule).IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(card => card.AccountId)
            .HasConstraintName("fk_credit_cards_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(card => card.AccountId)
            .HasDatabaseName("ix_credit_cards_account_id");

        builder.HasIndex(card => card.AccountId)
            .IsUnique()
            .HasFilter("is_archived = false")
            .HasDatabaseName("ux_credit_cards_account_id_active");
    }
}
