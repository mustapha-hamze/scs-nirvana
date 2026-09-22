using System;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Web.Tests;

// Swaps the real SQL Server-backed ApplicationDbContext for an isolated per-factory-instance
// InMemory database, so the Web pipeline (Identity, antiforgery, session, MVC filters) runs for
// real over HTTP without needing a live database.
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ValidateOnBuild used to be disabled here to work around a since-fixed registration gap
        // (CreateUserAttachmentHandler/GetUserAttachmentByIdHandler depended on the never-registered
        // generic IRepository<UserAttachment> instead of the registered IUserAttachmentRepository -
        // see UserAttachmentCompositionTests). Left enabled now so the whole test host's DI graph -
        // not just UserAttachment's - is eagerly validated on every Web.Tests run.
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

            // AddDbContext<T> doesn't replace a prior registration for the same T - each call
            // appends an IDbContextOptionsConfiguration<T>, and EF combines every registered one
            // when it builds the options. Without removing AddPersistence's original
            // UseSqlServer(...) configuration here too, the InMemory provider added below would
            // just be layered on top of it, and EF refuses to run with two providers at once.
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
