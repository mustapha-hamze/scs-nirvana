using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.CMSRepository;
using Application.CQRS.Handlers.ContentManagement.Category;
using Application.CQRS.Queries.ContentManagement.Category;
using Application.Mapper;
using Application.UnitOfWork;
using Application.UseCases.CMSServices;
using AutoMapper;
using Domains.Entities.ContentManagement;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Core.Tests.Architecture;

// Proves the Phase 2 use-case boundary cleanup: legacy Services.*/Core.Services.* namespaces are
// gone, the Category read no longer has two independent implementations, and the AutoMapper
// profile split (Domain<->Application.Contracts maps moved into Application) still resolves and
// validates through DI exactly like the single pre-split profile did.
public class ApplicationUseCaseBoundaryTests
{
    [Fact]
    public void ApplicationAssembly_HasNoLegacyServicesNamespace()
    {
        var offending = typeof(IUnitOfWork).Assembly.GetTypes()
            .Where(t => t.Namespace != null &&
                        (t.Namespace == "Services" || t.Namespace.StartsWith("Services.") || t.Namespace.StartsWith("Core.Services")))
            .ToList();

        Assert.True(offending.Count == 0,
            $"Found leftover legacy Services namespace type(s): {string.Join(", ", offending.Select(t => t.FullName))}.");
    }

    [Fact]
    public void GetCategoriesHandler_DelegatesToCategoryServices_NotDirectlyToRepository()
    {
        // Regression guard: the CQRS handler must resolve category reads through
        // ICategoryServices (the one implementation BackOffice controllers also call directly),
        // not reimplement its own repository + mapping logic side by side with CategoryServices.
        var constructorParams = typeof(GetCategoriesHandler).GetConstructors().Single().GetParameters();

        var parameter = Assert.Single(constructorParams);
        Assert.Equal(typeof(ICategoryServices), parameter.ParameterType);
    }

    [Fact]
    public async Task ServiceCollection_ResolvesGetCategoriesQuery_ThroughMediatRAndCategoryServices()
    {
        var categories = new List<Category>
        {
            new() { Id = 1, ApplicationId = 10, ParentId = 0, Title = "Root" },
            new() { Id = 2, ApplicationId = 10, ParentId = 1, Title = "Child" }
        };

        var repository = new Mock<ICategoryRepository>();
        repository.Setup(r => r.List(10, It.IsAny<CancellationToken>())).ReturnsAsync(categories);

        var services = new ServiceCollection();
        // AutoMapper's DI registration resolves ILoggerFactory internally when building the
        // mapper (MapperConfiguration now requires one) - the real host always has logging
        // registered, but this standalone ServiceCollection needs it added explicitly.
        services.AddLogging();
        services.AddSingleton(repository.Object);
        services.AddSingleton(Mock.Of<IUnitOfWork>());
        services.AddAutoMapper(cfg => cfg.AddProfile(new ApplicationMapperProfile()));
        services.AddTransient<ICategoryServices, CategoryServices>();
        services.AddMediatR(typeof(GetCategoriesHandler).Assembly);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new GetCategoriesQuery(10, 1));

        var dto = Assert.Single(result);
        Assert.Equal("Child", dto.Title);
    }

    [Fact]
    public void MapperConfiguration_BuildsAndMaps_AcrossApplicationAndInfrastructureProfiles()
    {
        // AssertConfigurationIsValid() is asserted separately, in
        // Core.Tests.Architecture.MappingConfigurationTests. What matters for the profile split
        // itself is that configuration still builds and a representative map from each profile
        // still produces the same field values it did as one profile.
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile(new Infrastructure.Mapper.MapperProfile());
            cfg.AddProfile(new ApplicationMapperProfile());
        }, NullLoggerFactory.Instance);
        var mapper = config.CreateMapper();

        var categoryDto = mapper.Map<Application.Contracts.CMS.CategoryDto>(
            new Category { Id = 1, ApplicationId = 10, Title = "Root" });
        Assert.Equal("Root", categoryDto.Title);

        var attachmentDto = mapper.Map<Infrastructure.Dto.CMSDtos.ContentAttachmentDto>(
            new Domains.Entities.ContentManagement.ContentAttachment { Id = 1, Title = "Doc" });
        Assert.Equal("Doc", attachmentDto.Title);
    }
}
