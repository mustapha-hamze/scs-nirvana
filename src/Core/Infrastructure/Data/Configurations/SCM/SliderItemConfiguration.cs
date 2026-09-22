using Domains.Entities.CustomModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.SCM;

public class SliderItemConfiguration : IEntityTypeConfiguration<SliderItem>
{
    public void Configure(EntityTypeBuilder<SliderItem> builder)
    {
        builder.ToTable("SCM_SliderItems");
        builder.ConfigureAudit();

        builder.Property(x => x.SliderId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(2048);
        builder.Property(x => x.Link).HasMaxLength(256);
        builder.Property(x => x.ImageFileName).HasMaxLength(128);

        builder.HasOne(x => x.Slider).WithMany(x => x.SliderItems).HasForeignKey(x => x.SliderId);
    }
}
