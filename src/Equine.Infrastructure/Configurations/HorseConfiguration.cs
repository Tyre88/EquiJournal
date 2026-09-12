using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class HorseConfiguration : IEntityTypeConfiguration<Horse>
{
    public void Configure(EntityTypeBuilder<Horse> builder)
    {
        builder.ToTable("horses");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Name).HasMaxLength(100).IsRequired();
        builder.Property(h => h.Species).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.Breed).HasMaxLength(100);
        builder.Property(h => h.Sex).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.Identity).HasMaxLength(100);
        builder.Property(h => h.Colour).HasMaxLength(100);
        builder.Property(h => h.Markings).HasMaxLength(500);
        builder.Property(h => h.StableLocation).HasMaxLength(200);
        builder.Property(h => h.StableAddress).HasMaxLength(200);
        builder.Property(h => h.StablePostcode).HasMaxLength(10);
        builder.Property(h => h.StableCity).HasMaxLength(100);
        builder.Property(h => h.Background).HasMaxLength(2000);
        builder.Property(h => h.EmailVerified).HasDefaultValue(true);
        builder.Property(h => h.Status).HasMaxLength(20).HasDefaultValue("Aktiv");
        builder.Property(h => h.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(h => h.UpdatedAt).HasDefaultValueSql("now()");
        builder.Property(h => h.DeletedAt);

        builder.HasIndex(h => h.TenantId);
        builder.HasIndex(h => h.OwnerId);
        builder.HasIndex(h => h.Name).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("ix_horses_name_trgm");

        builder.Ignore(h => h.IsDeleted);
    }
}
