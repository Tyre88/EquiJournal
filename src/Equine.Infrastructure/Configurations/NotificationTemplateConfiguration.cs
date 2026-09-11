using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(60);
        builder.Property(t => t.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Subject).HasMaxLength(300);
        builder.Property(t => t.Body).HasMaxLength(16000);
        builder.HasIndex(t => new { t.Type, t.Channel }).IsUnique();
    }
}
