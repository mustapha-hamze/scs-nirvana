using Domains.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

// Every BaseEntity-derived table shares the same key and audit-column shape. Centralizing it
// here means each IEntityTypeConfiguration only has to state what's actually distinct about
// that entity, and the PK/audit columns stay explicit (matching what convention already
// enforces today) without copy-pasting the same five lines into every configuration class.
public static class EntityTypeBuilderExtensions
{
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> builder) where T : BaseEntity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.UpdatedDT).IsRequired();
        builder.Property(x => x.CreatedDT).IsRequired();

        // Global soft-delete filter: every BaseEntity-derived query (including navigations
        // reached via Include/lazy-loading) excludes soft-deleted rows by default. A query that
        // must see soft-deleted rows on purpose (e.g. AddUserToApplication's restore check) has
        // to opt out explicitly with IgnoreQueryFilters() - and every one of those left in the
        // codebase must say why in a comment.
        //
        // Dapper/stored-procedure reads (e.g. SP_ContentsInCategory) run outside EF entirely and
        // are not affected by this filter - see IContentsInCategoryQueryAdapter.
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
