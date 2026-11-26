using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable(
            "accounts",
            table => table.HasCheckConstraint(
                "ck_accounts_tracking_start_day_one",
                "EXTRACT(DAY FROM tracking_start_date) = 1"));

        builder.HasKey(account => account.Id)
            .HasName("pk_accounts");

        builder.Property(account => account.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(account => account.OwnerUserId)
            .HasColumnName("owner_user_id")
            .IsRequired();

        builder.Property(account => account.Name)
            .HasColumnName("name")
            .HasMaxLength(DomainValidation.MaxNameLength)
            .IsRequired();

        builder.Property(account => account.InitialBalance)
            .HasColumnName("initial_balance")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(account => account.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(account => account.TrackingStartDate)
            .HasColumnName("tracking_start_date")
            .IsRequired();

        builder.Property(account => account.IsInitialBalanceConfirmed)
            .HasColumnName("is_initial_balance_confirmed")
            .IsRequired();

        builder.Ignore(account => account.TrackingStartPeriod);
        builder.Ignore(account => account.InitialBalanceDate);
        builder.Ignore(account => account.BalanceAdjustments);

        builder.HasMany<BalanceAdjustment>("_balanceAdjustments")
            .WithOne()
            .HasForeignKey(adjustment => adjustment.AccountId)
            .HasConstraintName("fk_balance_adjustments_accounts_account_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(account => account.OwnerUserId)
            .HasConstraintName("fk_accounts_users_owner_user_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(account => account.OwnerUserId)
            .HasDatabaseName("ix_accounts_owner_user_id");
    }
}
