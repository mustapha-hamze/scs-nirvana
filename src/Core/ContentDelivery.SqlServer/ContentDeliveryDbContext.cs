using Microsoft.EntityFrameworkCore;

namespace Cms.ContentDelivery.SqlServer;

// Read-only view of the existing CMS tables: only the columns delivery needs, mapped to the table
// and column names Core's ApplicationDbContext configures (ContentDeliverySqlAdapterTests checks
// they still match). Deliberately unmapped: FarsiContent, CMS_ContentTranslations, translation
// jobs and cultures - Phase 2 serves master/source text only. No query filters: every visibility
// rule (tenant, IsActive, IsDeleted) is explicit in SqlContentDeliveryClient.
internal sealed class ContentDeliveryDbContext : DbContext
{
    public ContentDeliveryDbContext(DbContextOptions<ContentDeliveryDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    public DbSet<ContentRow> Contents => Set<ContentRow>();
    public DbSet<MetadataRow> Metadata => Set<MetadataRow>();
    public DbSet<SectionRow> Sections => Set<SectionRow>();
    public DbSet<ElementRow> Elements => Set<ElementRow>();
    public DbSet<ImageRow> Images => Set<ImageRow>();
    public DbSet<CategoryRow> Categories => Set<CategoryRow>();
    public DbSet<TagRow> Tags => Set<TagRow>();
    public DbSet<ContentCategoryRow> ContentCategories => Set<ContentCategoryRow>();
    public DbSet<ContentTagRow> ContentTags => Set<ContentTagRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentRow>().ToTable("CMS_Contents");
        modelBuilder.Entity<MetadataRow>().ToTable("CMS_ContentMetadata");
        modelBuilder.Entity<SectionRow>().ToTable("CMS_ContentSections");
        modelBuilder.Entity<ElementRow>().ToTable("CMS_SectionElements");
        modelBuilder.Entity<ImageRow>().ToTable("CMS_ContentImages");
        modelBuilder.Entity<CategoryRow>().ToTable("CMS_Categories");
        modelBuilder.Entity<TagRow>().ToTable("GNR_Tags");
        modelBuilder.Entity<ContentCategoryRow>().ToTable("CMS_ContentInCategories");
        modelBuilder.Entity<ContentTagRow>().ToTable("CMS_ContentInTags");
    }

    // Delivery never writes; fail loudly rather than rely on nobody calling these.
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) => throw ReadOnly();

    private static InvalidOperationException ReadOnly() => new("The content delivery context is read-only.");
}

internal sealed class ContentRow
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public int TypeId { get; set; }
    public string? Title { get; set; }
    public string? HeadLine { get; set; }
    public string? Abstract { get; set; }
    public string? Description { get; set; }
    public DateTime PublishDt { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime UpdatedDT { get; set; }
}

internal sealed class MetadataRow
{
    public int Id { get; set; }
    public int ContentId { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Keywords { get; set; }
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class SectionRow
{
    public int Id { get; set; }
    public int ContentId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class ElementRow
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public int ElementType { get; set; }
    public string? ElementTitle { get; set; }
    public string? TinyText { get; set; }
    public string? EditorText { get; set; }
    public string? FileNameText { get; set; }
    public string? GalleryImages { get; set; }
    public int Size { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class ImageRow
{
    public int Id { get; set; }
    public int ContentId { get; set; }
    public string? ImageFileName { get; set; }
    public int Size { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class CategoryRow
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public int ParentId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class TagRow
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string? Title { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class ContentCategoryRow
{
    public int Id { get; set; }
    public int ContentId { get; set; }
    public int CategoryId { get; set; }
}

internal sealed class ContentTagRow
{
    public int Id { get; set; }
    public int ContentId { get; set; }
    public int TagId { get; set; }
}
