using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Web.Services.Translation;
using Xunit;

namespace Web.Tests;

public sealed class ContentTranslationWorkerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContentTranslationWorkerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Disabled by default: the host starts normally and the worker exits without touching the
    // (possibly not yet deployed) job table.
    [Fact]
    public async Task DisabledByDefault_HostStarts_AndWorkerExitsImmediately()
    {
        var worker = _factory.Services.GetServices<IHostedService>().OfType<ContentTranslationWorker>().Single();

        await worker.ExecuteTask!.WaitAsync(System.TimeSpan.FromSeconds(5));

        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }
}
