using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data
{
    public static class DBSetting
    {
        public static string ConnectionString()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);

            IConfiguration configuration = builder.Build();
            return configuration.GetValue<string>("ConnectionStrings:DefaultConnection");
        }

        public static string RedisConnectionString()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);

            IConfiguration configuration = builder.Build();
            return configuration.GetValue<string>("ConnectionStrings:RedisConnectionString");
        }
    }
}