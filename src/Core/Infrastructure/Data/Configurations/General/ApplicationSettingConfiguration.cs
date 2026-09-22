using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class ApplicationSettingConfiguration : IEntityTypeConfiguration<ApplicationSetting>
{
    public void Configure(EntityTypeBuilder<ApplicationSetting> builder)
    {
        builder.ToTable("GNR_ApplicationSettings");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.SettingId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.Value).HasMaxLength(512);

        builder.HasOne(x => x.Application).WithMany(x => x.ApplicationSettings).HasForeignKey(x => x.ApplicationId);
    }
}
