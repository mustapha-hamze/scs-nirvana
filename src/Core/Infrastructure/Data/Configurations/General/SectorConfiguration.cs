using Domains.Entities.AccessManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> builder)
    {
        builder.ToTable("AME_Sectors");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);

        builder.HasOne(x => x.Application).WithMany(x => x.Sectors).HasForeignKey(x => x.ApplicationId);
    }
}
