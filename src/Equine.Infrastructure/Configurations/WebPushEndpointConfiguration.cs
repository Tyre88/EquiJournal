using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class WebPushEndpointConfiguration : IEntityTypeConfiguration<WebPushEndpoint>
{
    public void Configure(EntityTypeBuilder<WebPushEndpoint> builder)
    {
        builder.ToTable("web_push_endpoints");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Endpoint).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.P256dh).HasMaxLength(255);
        builder.Property(e => e.Auth).HasMaxLength(255);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.Endpoint).IsUnique();
    }
}
