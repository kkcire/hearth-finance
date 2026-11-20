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

        builder.Property(income => income.ExpectedDate)
            .HasColumnName("expected_date")
            .IsRequired();

        builder.Property(income => income.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(income => income.Description)
            .HasColumnName("description")
            .HasMaxLength(DomainValidation.MaxDescriptionLength)
            .IsRequired();

        builder.Property(income => income.ReceivedOn)
            .HasColumnName("received_on");

        builder.Ignore(income => income.IsReceived);
        builder.Ignore(income => income.ExpectedMonth);
        builder.Ignore(income => income.ReceivedMonth);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(income => income.AccountId)
            .HasConstraintName("fk_incomes_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(income => new { income.AccountId, income.ExpectedDate })
            .HasDatabaseName("ix_incomes_account_id_expected_date");
    }
}
