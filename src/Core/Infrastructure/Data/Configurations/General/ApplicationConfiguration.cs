using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class ApplicationConfiguration : IEntityTypeConfiguration<Domains.Entities.General.Application>
{
    public void Configure(EntityTypeBuilder<Domains.Entities.General.Application> builder)
    {
        builder.ToTable("GNR_Applications");
        builder.ConfigureAudit();

        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.LogoFileName).HasMaxLength(450);
        builder.Property(x => x.ApplicationKey).HasMaxLength(128);
    }
}
