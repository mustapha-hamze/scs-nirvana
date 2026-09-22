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
        // The Development environment's default service provider validates the whole DI graph
        // eagerly on build. A pre-existing, unrelated Core/Application registration gap
        // (IRepository<UserAttachment> is never registered) then fails host startup before any
        // request runs. That gap is out of scope for these Web-only logout tests, so relax
        // eager validation for the test host rather than touching Core wiring.
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = false;
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
