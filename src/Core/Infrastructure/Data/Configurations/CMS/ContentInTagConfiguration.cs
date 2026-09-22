using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class ContentInTagConfiguration : IEntityTypeConfiguration<ContentInTag>
{
    public void Configure(EntityTypeBuilder<ContentInTag> builder)
    {
        builder.ToTable("CMS_ContentInTags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.TagId).IsRequired();
        builder.HasIndex(x => new { x.ContentId, x.TagId }).IsUnique();
        builder.HasOne<Content>().WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
    }
}
