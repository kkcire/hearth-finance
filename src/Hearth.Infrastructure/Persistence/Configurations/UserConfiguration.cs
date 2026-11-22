using Hearth.Domain;
using Hearth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hearth.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id)
            .HasName("pk_users");

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(user => user.Name)
            .HasColumnName("name")
            .HasMaxLength(DomainValidation.MaxNameLength)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(DomainValidation.MaxEmailLength)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");
    }
}
