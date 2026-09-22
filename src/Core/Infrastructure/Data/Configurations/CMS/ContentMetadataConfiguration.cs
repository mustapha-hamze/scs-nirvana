using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class ContentMetadataConfiguration : IEntityTypeConfiguration<ContentMetadata>
{
    public void Configure(EntityTypeBuilder<ContentMetadata> builder)
    {
        builder.ToTable("CMS_ContentMetadata");
        builder.ConfigureAudit();

        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(256);
        builder.Property(x => x.Author).HasMaxLength(128);
        builder.Property(x => x.Keywords).HasMaxLength(1024);
        builder.Property(x => x.Description).HasMaxLength(2048);

        // One-to-one: Content.Metadata is a single reference, not a collection.
        builder.HasOne(x => x.Content).WithOne(x => x.Metadata).HasForeignKey<ContentMetadata>(x => x.ContentId);
    }
}
