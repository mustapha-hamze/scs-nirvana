using Application.CMSRepository;
using Application.Mapper;
using Application.UnitOfWork;
using Application.UseCases.TranslatorServices;
using Core.Tests.TestSupport;
using Infrastructure.TranslatorServices;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Web.Extensions;
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

    // Uses the real Web registration methods; ValidateOnBuild fails if any registration in them
    // (including the backfill's own dependencies) can't be constructed.
    [Fact]
    public void WebCompositionMethods_ResolveTranslationBackfill()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=unused",
            ["OPENAI_API_KEY"] = "test-key"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAutoMapper(_ => { }, new[] { typeof(Infrastructure.Mapper.MapperProfile).Assembly, typeof(IUnitOfWork).Assembly }, ServiceLifetime.Singleton);
        services.AddPersistence(configuration).AddCmsServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<Infrastructure.CMSRepository.ContentTranslationBackfillRepository>(
            scope.ServiceProvider.GetRequiredService<IContentTranslationBackfillRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ContentTranslationBackfill>());

        Assert.IsType<Infrastructure.CMSRepository.ContentTranslationJobRepository>(
            scope.ServiceProvider.GetRequiredService<IContentTranslationJobRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ContentTranslationJobProcessor>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ContentTranslationRequests>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ManualContentTranslation>());
        Assert.IsType<Infrastructure.CMSRepository.LocalizedContentReadRepository>(
            scope.ServiceProvider.GetRequiredService<ILocalizedContentReadRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LocalizedContentReader>());
        Assert.False(scope.ServiceProvider.GetRequiredService<ContentTranslationOptions>().LocalizedReadEnabled);
        Assert.False(scope.ServiceProvider.GetRequiredService<ContentTranslationOptions>().WorkerEnabled);
        Assert.Contains(provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>(), s => s is Web.Services.Translation.ContentTranslationWorker);
    }
}
