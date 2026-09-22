using Domains.Entities.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.General;

public class UserInApplicationConfiguration : IEntityTypeConfiguration<UserInApplication>
{
    public void Configure(EntityTypeBuilder<UserInApplication> builder)
    {
        builder.ToTable("GNR_UserInApplications");
        builder.ConfigureAudit();

        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.ApplicationId).IsRequired();

        // One membership row per (user, application) - AddUserToApplication restores an existing
        // soft-deleted row instead of inserting a duplicate specifically to respect this.
        builder.HasIndex(x => new { x.UserId, x.ApplicationId }).IsUnique();
    }
}
