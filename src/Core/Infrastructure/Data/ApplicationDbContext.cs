using Domains.Entities.ContentManagement;
using Domains.Entities.General;
using Domains.Entities.User;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Domains.Entities.CustomModule;
using Domains.Entities.AccessManagement;

namespace Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        //public const string ConnectionString = "Data Source=65.109.7.113;Initial Catalog=___SCSDB_2023___;User Id=sa;Password=serDB2023@$8992;Trust Server Certificate=True;";
        //public const string ConnectionString = "Data Source=SQL5110.site4now.net;Initial Catalog=db_a9616e_scsdatabase;User Id=db_a9616e_scsdatabase_admin;Password=2023ABab$#;Trust Server Certificate=True;";
        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            builder.UseSqlServer(DBSetting.ConnectionString());

            base.OnConfiguring(builder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // General
            modelBuilder.Entity<Domains.Entities.General.Application>().ToTable("GNR_Applications");
            modelBuilder.Entity<SystemLog>().ToTable("GRN_SystemLogs");
            modelBuilder.Entity<Domains.Entities.General.Tag>().ToTable("GNR_Tags");
            modelBuilder.Entity<Culture>().ToTable("GNR_Cultures");
            modelBuilder.Entity<UserInApplication>().ToTable("GNR_UserInApplications");
            modelBuilder.Entity<ApplicationSetting>().ToTable("GNR_ApplicationSettings");
            modelBuilder.Entity<SystemType>().ToTable("GNR_SystemTypes");
            modelBuilder.Entity<Sector>().ToTable("AME_Sectors");
            modelBuilder.Entity<SectorEntity>().ToTable("AME_SectorEntities");
            modelBuilder.Entity<EntityAccess>().ToTable("AME_EntityAccesses");
            modelBuilder.Entity<UserAccess>().ToTable("GNR_UserAccesses");

            // CMS
            modelBuilder.Entity<Category>().ToTable("CMS_Categories");
            modelBuilder.Entity<Comment>().ToTable("CMS_Comments");
            modelBuilder.Entity<Content>().ToTable("CMS_Contents");
            modelBuilder.Entity<ContentImage>().ToTable("CMS_ContentImages");
            modelBuilder.Entity<ContentMetadata>().ToTable("CMS_ContentMetadata");
            modelBuilder.Entity<ContentSection>().ToTable("CMS_ContentSections");
            modelBuilder.Entity<Schema>().ToTable("CMS_Schema");
            modelBuilder.Entity<SchemaDetails>().ToTable("CMS_SchemaDetails");
            modelBuilder.Entity<SectionElement>().ToTable("CMS_SectionElements");
            modelBuilder.Entity<ContentInCategory>().ToTable("CMS_ContentInCategories");
            modelBuilder.Entity<ContentInTag>().ToTable("CMS_ContentInTags");
            modelBuilder.Entity<ContentInCulture>().ToTable("CMS_ContentInCultures");
            modelBuilder.Entity<ContentAttachment>().ToTable("CMS_ContentAttachments");
            modelBuilder.Entity<ContentAttachmentItem>().ToTable("CMS_ContentAttachmentItems");

            // SCM = System Custom Module
            modelBuilder.Entity<Domains.Entities.CustomModule.Slider>().ToTable("SCM_Sliders");
            modelBuilder.Entity<SliderItem>().ToTable("SCM_SliderItems");
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
        public DbSet<ContentAttachment> ContentAttachments { get; set; }
        public DbSet<ContentAttachmentItem> ContentAttachmentItems { get; set; }


        // SCM = System Custom Module
        public DbSet<Domains.Entities.CustomModule.Slider> Sliders { get; set; }
        public DbSet<SliderItem> SliderItems { get; set; }
    }
}
