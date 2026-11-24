using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseConditionConfiguration : IEntityTypeConfiguration<PurchaseCondition>
{
    public const string NormalizedNameShadowProperty = "NormalizedName";

    public void Configure(EntityTypeBuilder<PurchaseCondition> builder)
    {
        builder.ToTable("purchase_conditions");

        builder.HasKey(condition => condition.Id)
            .HasName("pk_purchase_conditions");

        builder.Property(condition => condition.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(condition => condition.OwnerUserId)
            .HasColumnName("owner_user_id");

        builder.Property(condition => condition.Name)
            .HasColumnName("name")
            .HasMaxLength(DomainValidation.MaxNameLength)
            .IsRequired();

        builder.Property(condition => condition.IsArchived)
            .HasColumnName("is_archived")
            .IsRequired();

        builder.Ignore(condition => condition.IsSystem);

        builder.Property<string>(NormalizedNameShadowProperty)
            .HasColumnName("normalized_name")
            .HasMaxLength(DomainValidation.MaxNameLength)
            .HasComputedColumnSql("lower(name)", stored: true);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(condition => condition.OwnerUserId)
            .HasConstraintName("fk_purchase_conditions_users_owner_user_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(condition => condition.OwnerUserId)
            .HasDatabaseName("ix_purchase_conditions_owner_user_id");

        builder.HasIndex(NormalizedNameShadowProperty)
            .IsUnique()
            .HasFilter("owner_user_id IS NULL")
            .HasDatabaseName("ux_purchase_conditions_system_normalized_name");

        builder.HasIndex(nameof(PurchaseCondition.OwnerUserId), NormalizedNameShadowProperty)
            .IsUnique()
            .HasFilter("owner_user_id IS NOT NULL")
            .HasDatabaseName("ux_purchase_conditions_owner_normalized_name");

        builder.HasData(
            SystemPurchaseConditions.Definitions.Select(item => new
            {
                item.Id,
                OwnerUserId = (Guid?)null,
                item.Name,
                IsArchived = false,
            }));
    }
}
