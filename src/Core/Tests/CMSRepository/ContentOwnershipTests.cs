using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Xunit;

namespace Core.Tests.CMSRepository;

// GetSectionForApplication / GetElementForApplication / GetContentMetadataForApplication resolve
// ownership through the actual parent chain (element -> section -> content -> application) and
// must reject a soft-deleted resource at any level exactly like a cross-application one — same
// "not found" outcome (SingleAsync throws) either way.
public class ContentOwnershipTests
{
    private static ContentRepository CreateRepository(Infrastructure.Data.ApplicationDbContext context)
    {
        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context);
        return new ContentRepository(context, TestConfiguration.Create(), unitOfWork);
    }

    private static (int contentId, int sectionId, int elementId, int metadataId) SeedFullChain(
        Infrastructure.Data.ApplicationDbContext context, int applicationId,
        bool contentDeleted = false, bool sectionDeleted = false, bool elementDeleted = false)
    {
        var content = new Content { ApplicationId = applicationId, TypeId = 1000, Title = "Sample", IsDeleted = contentDeleted };
        context.Contents.Add(content);
        context.SaveChanges();

        var section = new ContentSection { ContentId = content.Id, Priority = 1, IsDeleted = sectionDeleted };
        context.ContentSections.Add(section);
        context.SaveChanges();

        var element = new SectionElement { SectionId = section.Id, ElementType = 1000, TinyText = "Hello", IsDeleted = elementDeleted };
        context.SectionElements.Add(element);

        var metadata = new ContentMetadata { ContentId = content.Id, Title = "Meta" };
        context.ContentMetadatas.Add(metadata);
        context.SaveChanges();

        return (content.Id, section.Id, element.Id, metadata.Id);
    }

    // ---- GetSectionForApplication ----

    [Fact]
    public async Task GetSectionForApplication_SameApplication_ReturnsSection()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, sectionId, _, _) = SeedFullChain(context, applicationId: 1);

        var repository = CreateRepository(context);
        var section = await repository.GetSectionForApplication(sectionId, applicationId: 1);

        Assert.Equal(sectionId, section.Id);
    }

    [Fact]
    public async Task GetSectionForApplication_DifferentApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, sectionId, _, _) = SeedFullChain(context, applicationId: 1);

        var repository = CreateRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetSectionForApplication(sectionId, applicationId: 2));
    }

    [Fact]
    public async Task GetSectionForApplication_SoftDeletedSection_SameApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, sectionId, _, _) = SeedFullChain(context, applicationId: 1, sectionDeleted: true);

        var repository = CreateRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetSectionForApplication(sectionId, applicationId: 1));
    }

    [Fact]
    public async Task GetSectionForApplication_SoftDeletedParentContent_SameApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, sectionId, _, _) = SeedFullChain(context, applicationId: 1, contentDeleted: true);

        var repository = CreateRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetSectionForApplication(sectionId, applicationId: 1));
    }

    // ---- GetElementForApplication ----

    [Fact]
    public async Task GetElementForApplication_SameApplication_ReturnsElement()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, _, elementId, _) = SeedFullChain(context, applicationId: 1);

        var repository = CreateRepository(context);
        var element = await repository.GetElementForApplication(elementId, applicationId: 1);

        Assert.Equal(elementId, element.Id);
    }

    [Fact]
    public async Task GetElementForApplication_DifferentApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, _, elementId, _) = SeedFullChain(context, applicationId: 1);

        var repository = CreateRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetElementForApplication(elementId, applicationId: 2));
    }

    [Fact]
    public async Task GetElementForApplication_SoftDeletedElement_SameApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, _, elementId, _) = SeedFullChain(context, applicationId: 1, elementDeleted: true);

        var repository = CreateRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetElementForApplication(elementId, applicationId: 1));
    }

    [Fact]
    public async Task GetElementForApplication_SoftDeletedParentSection_SameApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, _, elementId, _) = SeedFullChain(context, applicationId: 1, sectionDeleted: true);

        var repository = CreateRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetElementForApplication(elementId, applicationId: 1));
    }

    [Fact]
    public async Task GetElementForApplication_SoftDeletedGrandparentContent_SameApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, _, elementId, _) = SeedFullChain(context, applicationId: 1, contentDeleted: true);

        var repository = CreateRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetElementForApplication(elementId, applicationId: 1));
    }

    // ---- GetContentMetadataForApplication ----

    [Fact]
    public async Task GetContentMetadataForApplication_SameApplication_ReturnsMetadata()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, _, _, metadataId) = SeedFullChain(context, applicationId: 1);

        var repository = CreateRepository(context);
        var metadata = await repository.GetContentMetadataForApplication(metadataId, applicationId: 1);

        Assert.Equal(metadataId, metadata.Id);
    }

    [Fact]
    public async Task GetContentMetadataForApplication_DifferentApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, _, _, metadataId) = SeedFullChain(context, applicationId: 1);

        var repository = CreateRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetContentMetadataForApplication(metadataId, applicationId: 2));
    }

    [Fact]
    public async Task GetContentMetadataForApplication_SoftDeletedParentContent_SameApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var (_, _, _, metadataId) = SeedFullChain(context, applicationId: 1, contentDeleted: true);

        var repository = CreateRepository(context);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetContentMetadataForApplication(metadataId, applicationId: 1));
    }
}
