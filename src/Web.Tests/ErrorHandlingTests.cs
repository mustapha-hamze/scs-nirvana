using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Application.UseCases.SCMServices;
using Domains.Entities.CustomModule;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Web.Tests;

// Covers the two pieces of error-handling behavior Program.cs/HomeController own directly: the
// public /Home/Error action never leaks anything sensitive regardless of verb, and an unhandled
// exception under /api/... gets an RFC 7807 JSON body (never the MVC HTML error page) once the
// app runs its real production pipeline (UseExceptionHandler, no Developer Exception Page).
public sealed class ErrorHandlingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ErrorHandlingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] SensitiveMarkers =
    {
        "Exception", "StackTrace", "at Web.", "at Application.", "at Infrastructure.",
        "ConnectionString", "localHost2024", "System.Threading",
    };

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task HomeError_AnyVerb_ReturnsGenericPageWithoutSensitiveDetails(string method)
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/Home/Error"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync();
        foreach (var marker in SensitiveMarkers)
            Assert.DoesNotContain(marker, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiException_InProduction_ReturnsProblemDetailsJson_WithoutSensitiveDetails()
    {
        const string boomMessage = "boom-for-test-should-not-leak-to-client";

        var productionFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISliderServices>();
                services.AddTransient<ISliderServices>(_ => new ThrowingSliderServices(boomMessage));
            });
        });
        var client = productionFactory.CreateClient();

        var response = await client.GetAsync("/api/Slider/1/GetSlider/1");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType ?? string.Empty);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":500", body.Replace(" ", string.Empty));
        Assert.Contains("traceId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(boomMessage, body);
        foreach (var marker in SensitiveMarkers)
            Assert.DoesNotContain(marker, body, StringComparison.OrdinalIgnoreCase);
    }

    // Only GetSliderWithItems is exercised by this test (via the public GET /api/Slider route);
    // every other member throws too so an accidental call surfaces immediately instead of
    // silently succeeding.
    private sealed class ThrowingSliderServices : ISliderServices
    {
        private readonly string _message;

        public ThrowingSliderServices(string message) => _message = message;

        public Task<Slider> GetSliderWithItems(int sliderId, int applicationId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(_message);

        public Task<Slider> Create(Slider slider, int applicationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_message);
        public Task<SliderItem> CreateSliderItem(SliderItem sliderItem, int applicationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_message);
        public Task DeactiveSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_message);
        public Task<List<Slider>> GetSliders(int applicationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_message);
        public Task<List<SliderItem>> GetSliderItems(int sliderId, int applicationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_message);
        public Task DeleteSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_message);
        public Task ActiveSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_message);
        public Task<SliderItem> GetSliderItem(int sliderItemId, int applicationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_message);
        public Task<SliderItem> UpdateSliderItem(SliderItem sliderItem, int applicationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException(_message);
    }
}
