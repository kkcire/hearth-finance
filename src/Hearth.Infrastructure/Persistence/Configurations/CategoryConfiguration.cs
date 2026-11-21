using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public const string NormalizedNameShadowProperty = "NormalizedName";

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(category => category.Id)
            .HasName("pk_categories");

        builder.Property(category => category.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(category => category.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(category => category.Name)
            .HasColumnName("name")
            .HasMaxLength(DomainValidation.MaxNameLength)
            .IsRequired();

        builder.Property(category => category.IsArchived)
            .HasColumnName("is_archived")
            .IsRequired();

        builder.Property<string>(NormalizedNameShadowProperty)
            .HasColumnName("normalized_name")
            .HasMaxLength(DomainValidation.MaxNameLength)
            .HasComputedColumnSql("lower(name)", stored: true);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(category => category.AccountId)
            .HasConstraintName("fk_categories_accounts_account_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(category => category.AccountId)
            .HasDatabaseName("ix_categories_account_id");

        builder.HasIndex(nameof(Category.AccountId), NormalizedNameShadowProperty)
            .IsUnique()
            .HasDatabaseName("ux_categories_account_id_normalized_name");
    }
}
