using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasColumnType("citext").HasMaxLength(80).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Plan).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.SubscriptionStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("ix_tenants_slug");
    }
}
