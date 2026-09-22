using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class CultureConfiguration : IEntityTypeConfiguration<Culture>
{
    public void Configure(EntityTypeBuilder<Culture> builder)
    {
        builder.ToTable("GNR_Cultures");
        builder.ConfigureAudit();

        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(64);
        builder.Property(x => x.Key).HasMaxLength(8);

        builder.HasOne(x => x.Application).WithMany(x => x.Cultures).HasForeignKey(x => x.ApplicationId);
    }
}
