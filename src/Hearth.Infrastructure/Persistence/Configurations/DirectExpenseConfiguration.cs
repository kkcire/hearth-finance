using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class DirectExpenseConfiguration : IEntityTypeConfiguration<DirectExpense>
{
    public void Configure(EntityTypeBuilder<DirectExpense> builder)
    {
        builder.ToTable("direct_expenses");

        builder.HasKey(expense => expense.Id)
            .HasName("pk_direct_expenses");

        builder.Property(expense => expense.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(expense => expense.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(expense => expense.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.Property(expense => expense.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(expense => expense.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(expense => expense.Description)
            .HasColumnName("description")
            .HasMaxLength(DomainValidation.MaxDescriptionLength)
            .IsRequired();

        builder.Ignore(expense => expense.Month);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(expense => expense.AccountId)
            .HasConstraintName("fk_direct_expenses_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(expense => expense.CategoryId)
            .HasConstraintName("fk_direct_expenses_categories_category_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(expense => new { expense.AccountId, expense.Date })
            .HasDatabaseName("ix_direct_expenses_account_id_date");

        builder.HasIndex(expense => expense.CategoryId)
            .HasDatabaseName("ix_direct_expenses_category_id");
    }
}
