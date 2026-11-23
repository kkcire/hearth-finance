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

        builder.Property(category => category.OwnerUserId)
            .HasColumnName("owner_user_id");

        builder.Property(category => category.Name)
            .HasColumnName("name")
            .HasMaxLength(DomainValidation.MaxNameLength)
            .IsRequired();

        builder.Property(category => category.IsArchived)
            .HasColumnName("is_archived")
            .IsRequired();

        builder.Ignore(category => category.IsSystem);

        builder.Property<string>(NormalizedNameShadowProperty)
            .HasColumnName("normalized_name")
            .HasMaxLength(DomainValidation.MaxNameLength)
            .HasComputedColumnSql("lower(name)", stored: true);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(category => category.OwnerUserId)
            .HasConstraintName("fk_categories_users_owner_user_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(category => category.OwnerUserId)
            .HasDatabaseName("ix_categories_owner_user_id");

        builder.HasIndex(NormalizedNameShadowProperty)
            .IsUnique()
            .HasFilter("owner_user_id IS NULL")
            .HasDatabaseName("ux_categories_system_normalized_name");

        builder.HasIndex(nameof(Category.OwnerUserId), NormalizedNameShadowProperty)
            .IsUnique()
            .HasFilter("owner_user_id IS NOT NULL")
            .HasDatabaseName("ux_categories_owner_normalized_name");

        builder.HasData(
            SystemCategories.Definitions.Select(item => new
            {
                item.Id,
                OwnerUserId = (Guid?)null,
                item.Name,
                IsArchived = false,
            }));
    }
}
