using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    public void Configure(EntityTypeBuilder<Owner> builder)
    {
        builder.ToTable("owners");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Email).HasColumnType("citext").HasMaxLength(255).IsRequired();
        builder.Property(o => o.Phone).HasMaxLength(50);
        builder.Property(o => o.AddressStreet).HasMaxLength(200);
        builder.Property(o => o.AddressPostcode).HasMaxLength(10);
        builder.Property(o => o.AddressCity).HasMaxLength(100);
        builder.Property(o => o.Notes).HasMaxLength(2000);
        builder.Property(o => o.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(o => o.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(o => o.DeletedAt);

        builder.Property(o => o.EmailVerified).HasDefaultValue(true);
        builder.Property(o => o.EmailInvalid).HasDefaultValue(false);

        builder.HasIndex(o => o.Email)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL")
            .HasDatabaseName("ix_owners_email_unique");

        builder.HasIndex(o => o.Name).HasMethod("gin").HasOperators("gin_trgm_ops").HasFilter("\"DeletedAt\" IS NULL").HasDatabaseName("ix_owners_name_trgm");
        builder.HasIndex(o => o.Phone).HasMethod("gin").HasOperators("gin_trgm_ops").HasFilter("\"DeletedAt\" IS NULL").HasDatabaseName("ix_owners_phone_trgm");

        builder.Ignore(o => o.IsDeleted);
        builder.HasQueryFilter(o => o.DeletedAt == null);
    }
}
