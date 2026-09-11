using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("notification_log");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(60);
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.Recipient).HasMaxLength(255);
        builder.Property(n => n.Subject).HasMaxLength(300);
        builder.Property(n => n.Body).HasMaxLength(16000);
        builder.Property(n => n.RelatedEntityType).HasMaxLength(80);
        builder.Property(n => n.Status).HasMaxLength(40);
        builder.Property(n => n.ProviderMessageId).HasMaxLength(200);
        builder.Property(n => n.ProviderResponse).HasMaxLength(4000);
        builder.HasIndex(n => n.RelatedEntityId);
        builder.HasIndex(n => n.ProviderMessageId);
        builder.HasIndex(n => n.CreatedAt);
    }
}
