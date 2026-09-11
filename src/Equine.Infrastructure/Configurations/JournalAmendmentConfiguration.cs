using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equine.Infrastructure.Configurations;

public class JournalAmendmentConfiguration : IEntityTypeConfiguration<JournalAmendment>
{
    public void Configure(EntityTypeBuilder<JournalAmendment> builder)
    {
        builder.ToTable("journal_amendments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Text).HasMaxLength(5000).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnType("uuid");
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(a => a.JournalEntryId);
    }
}
