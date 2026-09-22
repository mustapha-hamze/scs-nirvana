using AutoMapper;
using Xunit;

namespace Core.Tests.Architecture;

// AutoMapper's strict AssertConfigurationIsValid() is part of the architecture surface: every
// map must either cover its destination members or explicitly say why one is ignored, so a
// newly-added DTO/entity property can't silently go unmapped. See MapperProfile and
// ApplicationMapperProfile for the ignores this required.
public class MappingConfigurationTests
{
    [Fact]
    public void ApplicationProfile_ConfigurationIsValid()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new Application.Mapper.ApplicationMapperProfile()));

        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void InfrastructureProfile_ConfigurationIsValid()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new Infrastructure.Mapper.MapperProfile()));

        config.AssertConfigurationIsValid();
    }
}
