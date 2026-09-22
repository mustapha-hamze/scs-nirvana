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

        // One access row per (user, application) - SetUserAccesses already updates an existing
        // row (regardless of soft-delete state) instead of inserting a duplicate.
        builder.HasIndex(x => new { x.UserId, x.ApplicationId }).IsUnique();
    }
}
