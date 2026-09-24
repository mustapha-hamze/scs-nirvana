using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Domains.Entities.User;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.CustomModule;
using Domains.Entities.AccessManagement;

namespace Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly TimeProvider _timeProvider;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, TimeProvider timeProvider)
            : base(options)
        {
            _timeProvider = timeProvider;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Every non-Identity entity's table/keys/lengths/relations now live in one
            // IEntityTypeConfiguration<T> class per entity under Data/Configurations. That
            // includes the shared audit/soft-delete shape applied via ConfigureAudit<T>
            // (Data/Configurations/EntityTypeBuilderExtensions.cs), which is what gives every
            // BaseEntity-derived table its global "exclude soft-deleted rows" query filter.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }

        // Single Infrastructure-level mechanism for BaseEntity lifecycle policy: every
        // repository/use case just adds/mutates/removes entities as usual, and this is what
        // actually stamps CreatedDT/UpdatedDT (UTC, via the injected TimeProvider so tests can
        // fake "now") and converts a physical delete into a soft delete. No repository or use
        // case should set these fields or IsDeleted by hand any more.
        //
        // CreatedDT/UpdatedDT are only filled in when still at their default value, rather than
        // always overwritten, so seed/import/test code that deliberately backdates a row (e.g.
        // to assert ordering) keeps working - the common path (nothing set them) still gets a
        // real, server-controlled UTC timestamp instead of silently persisting default(DateTime).
        private void ApplyLifecyclePolicy()
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var entry in ChangeTracker.Entries<Domains.Entities.BaseEntity>())
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                }

                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.CreatedDT == default)
                        entry.Entity.CreatedDT = now;
                    if (entry.Entity.UpdatedDT == default)
                        entry.Entity.UpdatedDT = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedDT = now;
                }
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyLifecyclePolicy();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyLifecyclePolicy();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // General
        public DbSet<Domains.Entities.General.Application> Applications { get; set; }
        public DbSet<SystemLog> SystemLogs { get; set; }
        public DbSet<Domains.Entities.General.Tag> Tags { get; set; }
        public DbSet<Culture> Cultures { get; set; }
        public DbSet<UserInApplication> UserInApplications { get; set; }
        public DbSet<ApplicationSetting> ApplicationSettings { get; set; }
        public DbSet<SystemType> SystemTypes { get; set; }
        public DbSet<Sector> Sectors { get; set; }
        public DbSet<SectorEntity> SectorEntities { get; set; }
        public DbSet<EntityAccess> EntityAccesses { get; set; }
        public DbSet<UserAccess> UserAccesses { get; set; }
        public DbSet<UserAttachment> UserAttachments { get; set; }

        // CMS
        public DbSet<Category> Categories { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Content> Contents { get; set; }
        public DbSet<ContentImage> ContentImages { get; set; }
        public DbSet<ContentMetadata> ContentMetadatas { get; set; }
        public DbSet<ContentSection> ContentSections { get; set; }
        public DbSet<Schema> Schemas { get; set; }
        public DbSet<SchemaDetails> SchemaDetails { get; set; }
        public DbSet<SectionElement> SectionElements { get; set; }
        public DbSet<ContentInCategory> ContentInCategories { get; set; }
        public DbSet<ContentInTag> ContentInTags { get; set; }
        public DbSet<ContentInCulture> ContentInCultures { get; set; }
        public DbSet<ContentTranslation> ContentTranslations { get; set; }
        public DbSet<ContentAttachment> ContentAttachments { get; set; }
        public DbSet<ContentAttachmentItem> ContentAttachmentItems { get; set; }


        // SCM = System Custom Module
        public DbSet<Domains.Entities.CustomModule.Slider> Sliders { get; set; }
        public DbSet<SliderItem> SliderItems { get; set; }
    }
}
