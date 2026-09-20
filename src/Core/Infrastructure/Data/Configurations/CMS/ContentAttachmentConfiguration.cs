using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class ContentAttachmentConfiguration : IEntityTypeConfiguration<ContentAttachment>
{
    public void Configure(EntityTypeBuilder<ContentAttachment> builder)
    {
        builder.ToTable("CMS_ContentAttachments");
        builder.ConfigureAudit();

        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.Type).IsRequired();

        builder.HasOne(x => x.Content).WithMany().HasForeignKey(x => x.ContentId);
    }
}
