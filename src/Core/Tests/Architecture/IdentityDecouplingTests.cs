using System.Linq;
using Core.Tests.TestSupport;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Xunit;

namespace Core.Tests.Architecture;

public class IdentityDecouplingTests
{
    [Fact]
    public void Domain_HasNoAspNetCoreOrIdentityAssemblyReference()
    {
        var referenced = typeof(Domains.Entities.BaseEntity).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name);

        var violations = referenced
            .Where(name => name.StartsWith("Microsoft.AspNetCore") || name.Contains("Identity"))
            .ToList();

        Assert.True(violations.Count == 0,
            $"Domain must not reference ASP.NET Core/Identity assemblies, found: {string.Join(", ", violations)}.");
    }

    [Fact]
    public async Task Identity_StillResolvesThroughApplicationDbContext()
    {
        // ApplicationUser moved out of Domain into Infrastructure.Identity; this proves the
        // Identity store still reads/writes through ApplicationDbContext exactly as before.
        using var factory = new SqliteContextFactory();
        await using var context = factory.CreateContext();

        var store = new UserStore<ApplicationUser>(context);
        var user = new ApplicationUser
        {
            UserName = "identity-check@example.com",
            NormalizedUserName = "IDENTITY-CHECK@EXAMPLE.COM",
            Email = "identity-check@example.com",
            NormalizedEmail = "IDENTITY-CHECK@EXAMPLE.COM",
            FirstName = "Identity",
            LastName = "Check"
        };

        var createResult = await store.CreateAsync(user);
        Assert.True(createResult.Succeeded);

        await using var verifyContext = factory.CreateContext();
        var verifyStore = new UserStore<ApplicationUser>(verifyContext);
        var found = await verifyStore.FindByNameAsync("IDENTITY-CHECK@EXAMPLE.COM");

        Assert.NotNull(found);
        Assert.Equal("Identity", found.FirstName);
    }
}
