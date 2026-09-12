using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class ScheduledNotificationConfiguration : IEntityTypeConfiguration<ScheduledNotification>
{
    public void Configure(EntityTypeBuilder<ScheduledNotification> builder)
    {
        builder.ToTable("scheduled_notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(60);
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.Recipient).HasMaxLength(255).IsRequired();
        builder.Property(n => n.RelatedEntityType).HasMaxLength(80);
        builder.Property(n => n.PayloadJson).HasColumnType("jsonb");
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.LastError).HasMaxLength(2000);
        builder.Property(n => n.ProviderMessageId).HasMaxLength(200);
        builder.HasIndex(n => n.TenantId);
        builder.HasIndex(n => new { n.TenantId, n.Type, n.RelatedEntityId, n.Channel })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'")
            .HasDatabaseName("ix_scheduled_notifications_pending_unique");
        builder.HasIndex(n => new { n.Status, n.ScheduledFor });
    }
}
