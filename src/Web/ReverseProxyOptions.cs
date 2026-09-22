using System.Net;
using Microsoft.Extensions.Options;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace Web;

// Disabled by default (bound/validated in AddWebInfrastructure). When enabled, only requests
// whose immediate connection matches a configured KnownProxies/KnownNetworks entry are trusted to
// set X-Forwarded-For/X-Forwarded-Proto (see Program.cs); everything else keeps today's direct
// Kestrel/edge deployment behavior unchanged. Never enabled automatically by environment name -
// only by this explicit configuration.
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public bool Enabled { get; set; }

    // Comma-separated IP addresses, e.g. "10.0.0.5,10.0.0.6".
    public string KnownProxies { get; set; }

    // Comma-separated CIDR networks, e.g. "10.0.0.0/24".
    public string KnownNetworks { get; set; }

    public int ForwardLimit { get; set; } = 1;

    public IReadOnlyList<IPAddress> ParseKnownProxies() =>
        Split(KnownProxies).Select(IPAddress.Parse).ToList();

    public IReadOnlyList<IPNetwork> ParseKnownNetworks() =>
        Split(KnownNetworks).Select(ParseNetwork).ToList();

    public Microsoft.AspNetCore.Builder.ForwardedHeadersOptions ToForwardedHeadersOptions()
    {
        var options = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
        {
            ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
            ForwardLimit = ForwardLimit,
        };

        foreach (var proxy in ParseKnownProxies())
            options.KnownProxies.Add(proxy);
        foreach (var network in ParseKnownNetworks())
            options.KnownNetworks.Add(network);

        return options;
    }

    private static IPNetwork ParseNetwork(string value)
    {
        var parts = value.Split('/', 2);
        return new IPNetwork(IPAddress.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()));
    }

    private static IEnumerable<string> Split(string value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

// Fails fast (ValidateOnStart) instead of silently trusting nothing - or, worse, everything -
// when reverse-proxy support is turned on without a usable trust list.
public sealed class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
{
    public ValidateOptionsResult Validate(string name, ReverseProxyOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        if (options.ForwardLimit < 1)
            return ValidateOptionsResult.Fail(
                $"{ReverseProxyOptions.SectionName}:ForwardLimit must be at least 1 when reverse-proxy support is enabled.");

        IReadOnlyList<IPAddress> proxies;
        IReadOnlyList<IPNetwork> networks;
        try
        {
            proxies = options.ParseKnownProxies();
            networks = options.ParseKnownNetworks();
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(
                $"{ReverseProxyOptions.SectionName}:KnownProxies/{ReverseProxyOptions.SectionName}:KnownNetworks could not be parsed: {ex.Message}");
        }

        if (proxies.Count == 0 && networks.Count == 0)
            return ValidateOptionsResult.Fail(
                $"{ReverseProxyOptions.SectionName}:KnownProxies or {ReverseProxyOptions.SectionName}:KnownNetworks must list at least one trusted proxy when reverse-proxy support is enabled.");

        return ValidateOptionsResult.Success;
    }
}
