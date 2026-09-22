using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class SectionElementConfiguration : IEntityTypeConfiguration<SectionElement>
{
    public void Configure(EntityTypeBuilder<SectionElement> builder)
    {
        builder.ToTable("CMS_SectionElements");
        builder.ConfigureAudit();

        builder.Property(x => x.SectionId).IsRequired();
        builder.Property(x => x.ElementType).IsRequired();
        builder.Property(x => x.TinyText).HasMaxLength(256);
        builder.Property(x => x.FileNameText).HasMaxLength(256);
        builder.Property(x => x.GalleryImages).HasMaxLength(4092);
        builder.Property(x => x.Size).IsRequired();
        builder.Property(x => x.ElementTitle).HasMaxLength(256);

        builder.HasOne(x => x.Section).WithMany(x => x.Elements).HasForeignKey(x => x.SectionId);
    }
}
