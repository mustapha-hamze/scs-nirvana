using Core.Tests.TestSupport;
using Infrastructure.CMSRepository;
using Xunit;

namespace Core.Tests.CMSRepository;

public class ContentsInCategoryQueryAdapterTests
{
    [Fact]
    public async Task GetContentsInCategory_ConnectionFailure_ThrowsInsteadOfSwallowingAndReturningEmptyList()
    {
        // Regression guard: this used to open a long-lived SqlConnection field and swallow every
        // exception behind `catch (Exception) { return new List<ContentDto>(); }`, so a genuine
        // failure (bad connection string, unreachable server, broken stored procedure) was
        // indistinguishable from "no rows found". It must now propagate.
        var adapter = new ContentsInCategoryQueryAdapter(TestConfiguration.CreateUnreachable());

        await Assert.ThrowsAnyAsync<Exception>(() => adapter.GetContentsInCategory(categoryId: 1, applicationId: 1));
    }
}
