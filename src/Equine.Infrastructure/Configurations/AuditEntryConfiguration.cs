using Equine.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_log")
            .HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.ActorId)
            .HasMaxLength(255);

        builder.Property(a => a.Action)
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .HasMaxLength(255);

        builder.Property(a => a.Before)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(a => a.After)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(a => a.Timestamp)
            .HasDefaultValueSql("now()");
    }
}
