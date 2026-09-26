using System;
using System.Collections.Generic;
using Cms.ContentDelivery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.Tests.ContentDelivery;

public class ContentDeliveryContractTests
{
    [Fact]
    public void ListingQuery_DefaultsToFirstBoundedPage()
    {
        var query = new ContentListingQuery();

        Assert.Equal(1, query.PageNumber);
        Assert.InRange(query.PageSize, 1, ContentDeliveryLimits.MaxPageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ContentDeliveryLimits.MaxPageSize + 1)]
    public void ListingQuery_RejectsUnboundedPageSize(int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContentListingQuery { PageSize = pageSize });
    }

    [Fact]
    public void ListingQuery_RejectsPageNumberBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContentListingQuery { PageNumber = 0 });
    }

    [Fact]
    public void ListingQuery_WithRevalidatesChangedBounds()
    {
        var query = new ContentListingQuery { PageSize = ContentDeliveryLimits.MaxPageSize };

        Assert.Throws<ArgumentOutOfRangeException>(() => query with { PageSize = ContentDeliveryLimits.MaxPageSize + 1 });
    }

    [Fact]
    public void Result_CarriesValueOnlyWhenFound()
    {
        var summary = new ContentSummary { Id = 7 };

        Assert.Same(summary, ContentDeliveryResult<ContentSummary>.Found(summary).Value);
        Assert.Null(ContentDeliveryResult<ContentSummary>.NotFound().Value);
        Assert.Equal(ContentDeliveryStatus.InvalidCulture, ContentDeliveryResult<ContentSummary>.InvalidCulture().Status);
        Assert.Throws<ArgumentNullException>(() => ContentDeliveryResult<ContentSummary>.Found(null));
    }

    [Fact]
    public void Document_CollectionsDefaultToEmpty()
    {
        var document = new ContentDocument();

        Assert.Empty(document.Sections);
        Assert.Empty(document.Images);
        Assert.Empty(document.Categories);
        Assert.Empty(document.Tags);
    }
}

public class ContentDeliveryTenantConfigurationTests
{
    private static ServiceProvider Build(IConfiguration configuration) =>
        new ServiceCollection().AddContentDelivery(configuration).BuildServiceProvider();

    private static IConfigurationRoot Config(string applicationId) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(applicationId is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["ContentDelivery:ApplicationId"] = applicationId })
            .Build();

    [Fact]
    public void ValidConfiguration_BindsTheTenant()
    {
        using var provider = Build(Config("1111"));

        Assert.Equal(1111, provider.GetRequiredService<ContentDeliveryTenant>().ApplicationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    public void MissingOrNonPositiveApplicationId_FailsStartupValidation(string applicationId)
    {
        using var provider = Build(Config(applicationId));

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("ContentDelivery:ApplicationId", error.Message);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<ContentDeliveryTenant>());
    }

    [Fact]
    public void Tenant_IsFixedAtStartup_DespiteConfigurationReload()
    {
        var configuration = Config("1111");
        using var provider = Build(configuration);
        var tenant = provider.GetRequiredService<ContentDeliveryTenant>();

        configuration["ContentDelivery:ApplicationId"] = "2222";
        configuration.Reload();

        Assert.Same(tenant, provider.GetRequiredService<ContentDeliveryTenant>());
        Assert.Equal(1111, provider.GetRequiredService<ContentDeliveryTenant>().ApplicationId);
    }

    [Fact]
    public void SecondRegistration_IsRejected()
    {
        var services = new ServiceCollection().AddContentDelivery(Config("1111"));

        Assert.Throws<InvalidOperationException>(() => services.AddContentDelivery(Config("2222")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Tenant_RejectsNonPositiveApplicationId(int applicationId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContentDeliveryTenant(applicationId));
    }
}
