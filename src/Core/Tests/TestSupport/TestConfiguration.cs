using Microsoft.Extensions.Configuration;
using Moq;

namespace Core.Tests.TestSupport;

public static class TestConfiguration
{
    // ContentRepository takes IConfiguration only to build a Dapper SqlConnection for one
    // stored-procedure-backed method (GetContentsInCategory); most read-path tests never
    // exercise that path, so this just needs to satisfy the constructor without throwing.
    public static IConfiguration Create() => CreateWithConnectionString("Data Source=test;Initial Catalog=test;Integrated Security=true;");

    // A syntactically valid but unreachable connection string, with a short connect timeout so
    // tests that need a real (failing) SqlConnection.OpenAsync() don't hang on the ~15s default.
    public static IConfiguration CreateUnreachable() => CreateWithConnectionString("Data Source=unreachable-test-host;Initial Catalog=test;Integrated Security=true;Connect Timeout=1;TrustServerCertificate=true;");

    private static IConfiguration CreateWithConnectionString(string connectionString)
    {
        var section = new Mock<IConfigurationSection>();
        section.Setup(s => s["DefaultConnection"]).Returns(connectionString);

        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c.GetSection("ConnectionStrings")).Returns(section.Object);

        return configuration.Object;
    }
}
