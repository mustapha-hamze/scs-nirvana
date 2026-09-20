using Domains.Entities.CustomModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.SCM;

public class SliderConfiguration : IEntityTypeConfiguration<Slider>
{
    public void Configure(EntityTypeBuilder<Slider> builder)
    {
        builder.ToTable("SCM_Sliders");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);

        builder.HasOne(x => x.Application).WithMany(x => x.Sliders).HasForeignKey(x => x.ApplicationId);
    }
}
