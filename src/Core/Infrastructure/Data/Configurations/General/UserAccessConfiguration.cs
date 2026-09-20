using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class UserAccessConfiguration : IEntityTypeConfiguration<UserAccess>
{
    public void Configure(EntityTypeBuilder<UserAccess> builder)
    {
        builder.ToTable("GNR_UserAccesses");
        builder.ConfigureAudit();

        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.ApplicationId).IsRequired();
        builder.Property(x => x.Access).HasMaxLength(4096);
    }
}
