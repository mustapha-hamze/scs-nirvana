using Microsoft.Extensions.Configuration;
using Moq;

namespace Core.Tests.TestSupport;

public static class TestConfiguration
{
    // ContentRepository takes IConfiguration only to build a Dapper SqlConnection for one
    // stored-procedure-backed method (GetContentsInCategory); the read-path tests never
    // exercise that path, so this just needs to satisfy the constructor without throwing.
    public static IConfiguration Create()
    {
        var section = new Mock<IConfigurationSection>();
        section.Setup(s => s["DefaultConnection"]).Returns("Data Source=test;Initial Catalog=test;Integrated Security=true;");

        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c.GetSection("ConnectionStrings")).Returns(section.Object);

        return configuration.Object;
    }
}
