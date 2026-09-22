using Domains.Entities.ContentManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.CMS;

public class ContentInCategoryConfiguration : IEntityTypeConfiguration<ContentInCategory>
{
    public void Configure(EntityTypeBuilder<ContentInCategory> builder)
    {
        builder.ToTable("CMS_ContentInCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.CategoryId).IsRequired();
        builder.HasIndex(x => new { x.ContentId, x.CategoryId }).IsUnique();
        builder.HasOne<Content>().WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}
