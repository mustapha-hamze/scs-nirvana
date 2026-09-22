using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class ContentImageConfiguration : IEntityTypeConfiguration<ContentImage>
{
    public void Configure(EntityTypeBuilder<ContentImage> builder)
    {
        builder.ToTable("CMS_ContentImages");
        builder.ConfigureAudit();

        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.ImageFileName).HasMaxLength(128);
        builder.Property(x => x.Size).IsRequired();

        builder.HasOne(x => x.Content).WithMany(x => x.Images).HasForeignKey(x => x.ContentId);
    }
}
