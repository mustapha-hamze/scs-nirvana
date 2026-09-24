using Application.CQRS.Command.UserManagement;
using Application.CQRS.Queries.UserManagement;
using Application.Contracts.UserManagement;
using Application.UnitOfWork;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Web.Extensions;
using Xunit;

namespace Core.Tests.Architecture;

// CreateUserAttachmentHandler/GetUserAttachmentByIdHandler used to inject the generic
// IRepository<UserAttachment>, which nothing registers (only the typed IUserAttachmentRepository
// -> UserAttachmentRepository binding exists, in Web.Extensions.ServiceCollectionExtensions.
// AddPersistence). That gap only surfaced at first request time - MediatR resolves handlers
// lazily - so it shipped past every prior build and every prior test run. This wires the real
// AddPersistence/AddMediatR registration (not a hand-rolled substitute) with ValidateOnBuild so a
// reintroduced IRepository<UserAttachment>/IRepository<T> dependency on either handler fails the
// test immediately instead of at first request.
public class UserAttachmentCompositionTests
{
    private static IServiceCollection BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=UserAttachmentCompositionTests;Trusted_Connection=True;"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddPersistence(configuration);
        services.AddCmsServices();
        services.AddAutoMapper(_ => { }, new[] { typeof(Infrastructure.Mapper.MapperProfile).Assembly, typeof(IUnitOfWork).Assembly }, ServiceLifetime.Singleton);
        services.AddMediatR(typeof(IUnitOfWork).Assembly);

        return services;
    }

    [Fact]
    public void ServiceCollection_BuildsWithValidateOnBuild_NoUnregisteredHandlerDependencies()
    {
        using var provider = BuildServices().BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(provider);
    }

    [Fact]
    public void ServiceCollection_ResolvesBothUserAttachmentHandlers_ThroughTypedRepository()
    {
        using var provider = BuildServices().BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRequestHandler<CreateUserAttachmentCommand, Unit>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRequestHandler<GetUserAttachmentByIdQuery, UserAttachmentDto>>());
    }

    // Migration-specific: AutoMapper 13+ moved AddAutoMapper into the core package and made the
    // Action<IMapperConfigurationExpression> parameter mandatory (previously supplied by the now
    // abandoned AutoMapper.Extensions.Microsoft.DependencyInjection package). This proves the new
    // call shape still honors the explicit ServiceLifetime.Singleton argument, not just that it
    // compiles.
    [Fact]
    public void ServiceCollection_ResolvesIMapper_AsSingleton()
    {
        using var provider = BuildServices().BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var mapper1 = scope1.ServiceProvider.GetRequiredService<IMapper>();
        var mapper2 = scope2.ServiceProvider.GetRequiredService<IMapper>();

        Assert.Same(mapper1, mapper2);
    }
}
