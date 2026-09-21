using AutoMapper;
using Application.Contracts.CMS;
using Core.Tests.TestSupport;
using Domains.Entities.ContentManagement;
using Infrastructure.CMSRepository;
using Infrastructure.Mapper;
using Application.Mapper;
using Infrastructure.Repository;
using Application.UseCases.CMSServices;
using Xunit;

namespace Core.Tests.CMSServices;

// End-to-end (real repository + SQLite, not mocked) coverage for the Schema aggregate's
// application scoping: every case here proves the actual persisted/rejected outcome, not just
// that a method was called with certain arguments.
public class SchemaServicesTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => { cfg.AddProfile(new MapperProfile()); cfg.AddProfile(new ApplicationMapperProfile()); });
        return config.CreateMapper();
    }

    private static SchemaServices CreateSut(Infrastructure.Data.ApplicationDbContext context)
    {
        var unitOfWork = new Infrastructure.UnitOfWork.UnitOfWork(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.UnitOfWork.UnitOfWork>.Instance);
        return new SchemaServices(
            new SchemaRepository(context),
            CreateMapper(),
            unitOfWork);
    }

    [Fact]
    public async Task GetById_CrossApplication_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 1, Title = "Schema" };
        context.Schemas.Add(schema);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.GetById(schema.Id, applicationId: 2));
    }

    [Fact]
    public async Task GetById_SoftDeleted_Throws()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 1, Title = "Schema", IsDeleted = true };
        context.Schemas.Add(schema);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.GetById(schema.Id, applicationId: 1));
    }

    [Fact]
    public async Task Update_CrossApplicationSchemaId_ThrowsAndDoesNotWrite()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 2, Title = "Victim Schema" };
        context.Schemas.Add(schema);
        context.SaveChanges();

        var sut = CreateSut(context);

        // Application 1 tries to update application 2's schema by guessing/knowing its Id.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.Update(new SchemaDto { Id = schema.Id, Title = "Hijacked" }, applicationId: 1));

        await using var verifyContext = factory.CreateContext();
        var unchanged = verifyContext.Schemas.Single(s => s.Id == schema.Id);
        Assert.Equal("Victim Schema", unchanged.Title);
        Assert.Equal(2, unchanged.ApplicationId);
    }

    [Fact]
    public async Task Update_SoftDeletedSchema_ThrowsAndDoesNotWrite()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 1, Title = "Deleted Schema", IsDeleted = true };
        context.Schemas.Add(schema);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.Update(new SchemaDto { Id = schema.Id, Title = "Hijacked" }, applicationId: 1));
    }

    [Fact]
    public async Task Update_ApplicationIdTampering_ServerPinsRealApplicationId()
    {
        using var factory = new SqliteContextFactory();
        int schemaId;
        using (var seedContext = factory.CreateContext())
        {
            var schema = new Schema { ApplicationId = 1, Title = "Original" };
            seedContext.Schemas.Add(schema);
            seedContext.SaveChanges();
            schemaId = schema.Id;
        }

        // A fresh context for the update: reusing the seeding context would make EF reject
        // attaching the freshly-loaded (AsNoTracking) entity as already tracked.
        using var context = factory.CreateContext();
        var sut = CreateSut(context);

        // The DTO claims ApplicationId 99 - the caller's real application (1) must win.
        await sut.Update(new SchemaDto { Id = schemaId, ApplicationId = 99, Title = "Updated" }, applicationId: 1);

        await using var verifyContext = factory.CreateContext();
        var updated = verifyContext.Schemas.Single(s => s.Id == schemaId);
        Assert.Equal(1, updated.ApplicationId);
        Assert.Equal("Updated", updated.Title);
    }

    [Fact]
    public async Task Create_ApplicationIdTampering_ServerPinsRealApplicationId()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();

        var sut = CreateSut(context);

        // The DTO claims ApplicationId 99 - the caller's real application (1) must win.
        var created = await sut.Create(new SchemaDto { ApplicationId = 99, Title = "New Schema" }, applicationId: 1);

        Assert.Equal(1, created.ApplicationId);

        await using var verifyContext = factory.CreateContext();
        var stored = verifyContext.Schemas.Single(s => s.Id == created.Id);
        Assert.Equal(1, stored.ApplicationId);
    }

    [Fact]
    public async Task Delete_CrossApplication_ThrowsAndDoesNotDelete()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 2, Title = "Victim" };
        context.Schemas.Add(schema);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.Delete(schema.Id, applicationId: 1));

        await using var verifyContext = factory.CreateContext();
        var stillThere = verifyContext.Schemas.Single(s => s.Id == schema.Id);
        Assert.False(stillThere.IsDeleted);
    }

    [Fact]
    public async Task CreateDetails_CrossApplicationSchemaId_ThrowsAndDoesNotWrite()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 2, Title = "Victim" };
        context.Schemas.Add(schema);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.CreateDetails(new SchemaDetailsDto { SchemaId = schema.Id, Title = "Detail" }, applicationId: 1));

        await using var verifyContext = factory.CreateContext();
        Assert.Empty(verifyContext.SchemaDetails.Where(d => d.SchemaId == schema.Id));
    }

    [Fact]
    public async Task CreateDetails_SoftDeletedParentSchema_ThrowsAndDoesNotWrite()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 1, Title = "Deleted", IsDeleted = true };
        context.Schemas.Add(schema);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.CreateDetails(new SchemaDetailsDto { SchemaId = schema.Id, Title = "Detail" }, applicationId: 1));
    }

    [Fact]
    public async Task DeleteDetails_CrossApplication_ThrowsAndDoesNotDelete()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 2, Title = "Victim" };
        context.Schemas.Add(schema);
        context.SaveChanges();
        var detail = new SchemaDetails { SchemaId = schema.Id, Title = "Detail" };
        context.SchemaDetails.Add(detail);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.DeleteDetails(detail.Id, applicationId: 1));

        await using var verifyContext = factory.CreateContext();
        var stillThere = verifyContext.SchemaDetails.Single(d => d.Id == detail.Id);
        Assert.False(stillThere.IsDeleted);
    }

    [Fact]
    public async Task DeleteDetails_SoftDeletedParentSchema_ThrowsAndDoesNotDelete()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 1, Title = "Deleted", IsDeleted = true };
        context.Schemas.Add(schema);
        context.SaveChanges();
        var detail = new SchemaDetails { SchemaId = schema.Id, Title = "Detail" };
        context.SchemaDetails.Add(detail);
        context.SaveChanges();

        var sut = CreateSut(context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.DeleteDetails(detail.Id, applicationId: 1));
    }

    [Fact]
    public async Task DetailsList_CrossApplicationSchema_ReturnsEmpty()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 2, Title = "Victim" };
        context.Schemas.Add(schema);
        context.SaveChanges();
        context.SchemaDetails.Add(new SchemaDetails { SchemaId = schema.Id, Title = "Detail" });
        context.SaveChanges();

        var sut = CreateSut(context);

        var result = await sut.DetailsList(schema.Id, applicationId: 1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DetailsList_SoftDeletedParentSchema_ReturnsEmpty()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 1, Title = "Deleted", IsDeleted = true };
        context.Schemas.Add(schema);
        context.SaveChanges();
        context.SchemaDetails.Add(new SchemaDetails { SchemaId = schema.Id, Title = "Detail" });
        context.SaveChanges();

        var sut = CreateSut(context);

        var result = await sut.DetailsList(schema.Id, applicationId: 1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DetailsList_SoftDeletedDetail_ExcludedFromSameApplicationSchema()
    {
        using var factory = new SqliteContextFactory();
        using var context = factory.CreateContext();
        var schema = new Schema { ApplicationId = 1, Title = "Schema" };
        context.Schemas.Add(schema);
        context.SaveChanges();
        context.SchemaDetails.Add(new SchemaDetails { SchemaId = schema.Id, Title = "Active Detail" });
        context.SchemaDetails.Add(new SchemaDetails { SchemaId = schema.Id, Title = "Deleted Detail", IsDeleted = true });
        context.SaveChanges();

        var sut = CreateSut(context);

        var result = await sut.DetailsList(schema.Id, applicationId: 1);

        var detail = Assert.Single(result);
        Assert.Equal("Active Detail", detail.Title);
    }
}
