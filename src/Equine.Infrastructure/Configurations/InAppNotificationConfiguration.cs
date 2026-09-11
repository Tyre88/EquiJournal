using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class InAppNotificationConfiguration : IEntityTypeConfiguration<InAppNotification>
{
    public void Configure(EntityTypeBuilder<InAppNotification> builder)
    {
        builder.ToTable("in_app_notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(60);
        builder.Property(n => n.Title).HasMaxLength(200);
        builder.Property(n => n.Body).HasMaxLength(2000);
        builder.Property(n => n.Link).HasMaxLength(400);
        builder.HasIndex(n => new { n.UserId, n.ReadAt });
    }
}
