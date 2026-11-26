using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class BalanceAdjustmentConfiguration : IEntityTypeConfiguration<BalanceAdjustment>
{
    public void Configure(EntityTypeBuilder<BalanceAdjustment> builder)
    {
        builder.ToTable("balance_adjustments");

        builder.HasKey(adjustment => adjustment.Id)
            .HasName("pk_balance_adjustments");

        builder.Property(adjustment => adjustment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(adjustment => adjustment.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(adjustment => adjustment.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(adjustment => adjustment.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(adjustment => adjustment.Reason)
            .HasColumnName("reason")
            .HasMaxLength(DomainValidation.MaxDescriptionLength);

        builder.Property(adjustment => adjustment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(adjustment => new { adjustment.AccountId, adjustment.Date })
            .HasDatabaseName("ix_balance_adjustments_account_id_date");
    }
}
