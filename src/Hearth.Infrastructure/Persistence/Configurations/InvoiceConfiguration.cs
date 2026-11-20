using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(invoice => invoice.Id)
            .HasName("pk_invoices");

        builder.Property(invoice => invoice.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(invoice => invoice.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(invoice => invoice.ChargedAmount)
            .HasColumnName("charged_amount")
            .HasColumnType("numeric(18,2)");

        builder.Property(invoice => invoice.PaidOn)
            .HasColumnName("paid_on");

        builder.OwnsOne(invoice => invoice.Cycle, cycle =>
        {
            cycle.Property(c => c.ClosingMonth)
                .HasColumnName("closing_month")
                .IsRequired();

            cycle.OwnsOne(c => c.Rule, rule =>
            {
                rule.Property(r => r.ClosingDay)
                    .HasColumnName("closing_day")
                    .IsRequired();

                rule.Property(r => r.DueDay)
                    .HasColumnName("due_day")
                    .IsRequired();
            });

            cycle.Navigation(c => c.Rule).IsRequired();
        });

        builder.Navigation(invoice => invoice.Cycle).IsRequired();

        builder.Ignore(invoice => invoice.IsPaid);
        builder.Ignore(invoice => invoice.PaidMonth);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(invoice => invoice.AccountId)
            .HasConstraintName("fk_invoices_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(invoice => invoice.AccountId)
            .HasDatabaseName("ix_invoices_account_id");
    }
}
