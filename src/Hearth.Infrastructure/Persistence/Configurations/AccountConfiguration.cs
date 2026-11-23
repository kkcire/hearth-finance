using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

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

        builder.Property(account => account.InitialBalanceDate)
            .HasColumnName("initial_balance_date")
            .IsRequired();

        builder.Property(account => account.IsInitialBalanceConfirmed)
            .HasColumnName("is_initial_balance_confirmed")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(account => account.OwnerUserId)
            .HasConstraintName("fk_accounts_users_owner_user_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(account => account.OwnerUserId)
            .HasDatabaseName("ix_accounts_owner_user_id");
    }
}
