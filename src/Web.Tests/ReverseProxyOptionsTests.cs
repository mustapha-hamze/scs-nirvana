using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web;
using Xunit;

namespace Web.Tests;

// ReverseProxyOptions is disabled by default and, when enabled, is validated at startup
// (ValidateOnStart, wired in AddWebInfrastructure) rather than silently trusting nothing or
// trusting every proxy. The Validate_* tests exercise ReverseProxyOptionsValidator directly for
// every rule; the last two boot the real host to prove that validator is actually wired in.
public sealed class ReverseProxyOptionsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ReverseProxyOptionsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Validate_Disabled_IsValid_RegardlessOfMissingTrustConfig()
    {
        var options = new ReverseProxyOptions { Enabled = false };

        var result = new ReverseProxyOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EnabledWithNoKnownProxiesOrNetworks_Fails()
    {
        var options = new ReverseProxyOptions { Enabled = true };

        var result = new ReverseProxyOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_EnabledWithForwardLimitBelowOne_Fails()
    {
        var options = new ReverseProxyOptions { Enabled = true, KnownProxies = "10.0.0.5", ForwardLimit = 0 };

        var result = new ReverseProxyOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_EnabledWithUnparsableKnownProxies_Fails()
    {
        var options = new ReverseProxyOptions { Enabled = true, KnownProxies = "not-an-ip" };

        var result = new ReverseProxyOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_EnabledWithKnownProxy_Succeeds()
    {
        var options = new ReverseProxyOptions { Enabled = true, KnownProxies = "10.0.0.5, 10.0.0.6" };

        var result = new ReverseProxyOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { IPAddress.Parse("10.0.0.5"), IPAddress.Parse("10.0.0.6") }, options.ParseKnownProxies());
    }

    [Fact]
    public void Validate_EnabledWithKnownNetwork_Succeeds()
    {
        var options = new ReverseProxyOptions { Enabled = true, KnownNetworks = "10.0.0.0/24" };

        var result = new ReverseProxyOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
        var network = Assert.Single(options.ParseKnownNetworks());
        Assert.Equal(IPAddress.Parse("10.0.0.0"), network.BaseAddress);
        Assert.Equal(24, network.PrefixLength);
    }

    [Fact]
    public void ToForwardedHeadersOptions_AddsConfiguredProxyAndNetwork_WithoutClearingTheDefaultTrustList()
    {
        var options = new ReverseProxyOptions
        {
            Enabled = true,
            KnownProxies = "10.0.0.5",
            KnownNetworks = "10.0.1.0/24",
            ForwardLimit = 2,
        };

        var forwardedHeadersOptions = options.ToForwardedHeadersOptions();

        // ForwardedHeadersOptions itself already seeds loopback by default (::1 in KnownProxies,
        // 127.0.0.0/8 in KnownIPNetworks); this only proves the configured proxy/network were
        // added on top of that, never that either list was cleared to empty.
        Assert.Contains(IPAddress.Parse("10.0.0.5"), forwardedHeadersOptions.KnownProxies);
        Assert.Contains(IPAddress.Parse("::1"), forwardedHeadersOptions.KnownProxies);
        Assert.Contains(new IPNetwork(IPAddress.Parse("10.0.1.0"), 24), forwardedHeadersOptions.KnownIPNetworks);
        Assert.Contains(new IPNetwork(IPAddress.Parse("127.0.0.0"), 8), forwardedHeadersOptions.KnownIPNetworks);
        Assert.Equal(2, forwardedHeadersOptions.ForwardLimit);
    }

    [Fact]
    public void DisabledByDefault_NoConfigurationOverride_OptionsBindToDisabled()
    {
        var options = _factory.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;

        Assert.False(options.Enabled);
    }

    [Fact]
    public async Task EnabledWithoutTrustList_FailsHostStartup_NotJustTheStandaloneValidator()
    {
        // Proves ValidateOnStart (wired in AddWebInfrastructure) actually runs during host
        // startup for this option, not only when ReverseProxyOptionsValidator is called directly.
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["ReverseProxy:Enabled"] = "true" })));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }
}
