using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class ContentAttachmentItemConfiguration : IEntityTypeConfiguration<ContentAttachmentItem>
{
    public void Configure(EntityTypeBuilder<ContentAttachmentItem> builder)
    {
        builder.ToTable("CMS_ContentAttachmentItems");
        builder.ConfigureAudit();

        builder.Property(x => x.AttachmentId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(1024);
        builder.Property(x => x.FileName).HasMaxLength(512);

        builder.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
    }
}
