using Application.Contracts.Tenancy;
using Xunit;

namespace Core.Tests.UserManagement;

public class CurrentApplicationContextExtensionsTests
{
    private class FakeCurrentApplicationContext : ICurrentApplicationContext
    {
        public int? CurrentApplicationId { get; set; }
    }

    [Fact]
    public void RequireApplicationId_Selected_ReturnsIt()
    {
        var context = new FakeCurrentApplicationContext { CurrentApplicationId = 7 };

        Assert.Equal(7, context.RequireApplicationId());
    }

    [Fact]
    public void RequireApplicationId_NoneSelected_Throws()
    {
        var context = new FakeCurrentApplicationContext();

        Assert.Throws<InvalidOperationException>(() => context.RequireApplicationId());
    }
}
