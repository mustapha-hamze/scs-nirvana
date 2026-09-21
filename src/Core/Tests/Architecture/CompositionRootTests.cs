using Application.Mapper;
using Application.UnitOfWork;
using Core.Tests.TestSupport;
using Infrastructure.TranslatorServices;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using UnitOfWorkImpl = Infrastructure.UnitOfWork.UnitOfWork;

namespace Core.Tests.Architecture;

// Phase 4: proves the pieces this phase added to the composition root - ILogger<T> injection at
// the OpenAiTranslationPort/UnitOfWork Infrastructure boundaries, plus the already-split
// AutoMapper profiles and MediatR - all resolve together through one DI container, the same way
// the real Web host wires them (AddLogging/AddOptions/AddAutoMapper/AddMediatR), without a
// missing-service exception at startup.
public class CompositionRootTests
{
    [Fact]
    public void ServiceCollection_ResolvesLoggingOptionsMapperAndMediatR_Together()
    {
        using var dbFactory = new SqliteContextFactory();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(dbFactory.CreateContext());
        services.Configure<OpenAiTranslationOptions>(o => o.ApiKey = "test-key");
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile(new Infrastructure.Mapper.MapperProfile());
            cfg.AddProfile(new ApplicationMapperProfile());
        });
        services.AddScoped<IUnitOfWork, UnitOfWorkImpl>();
        services.AddTransient<OpenAiTranslationPort>();
        services.AddMediatR(typeof(IUnitOfWork).Assembly);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OpenAiTranslationPort>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISender>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOptions<OpenAiTranslationOptions>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILogger<UnitOfWorkImpl>>());
    }
}
