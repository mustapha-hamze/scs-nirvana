#nullable enable
using Cms.ContentDelivery;
using Xunit;

namespace Core.Tests.ContentDelivery;

// Written the way a nullable-enabled website consumes the SDK. Core.Tests turns nullable warnings
// into errors, so this file compiling proves the guarded reads below need no null-forgiving
// operator; ContentDeliveryBoundaryTests proves unguarded Value reads are annotated nullable.
public class ContentDeliveryNullableUsageTests
{
    private static int ReadGuardedByIsFound(ContentDeliveryResult<ContentSummary> result) =>
        result.IsFound ? result.Value.Id : -1;

    private static int ReadGuardedByTryGetValue(ContentDeliveryResult<ContentSummary> result) =>
        result.TryGetValue(out var summary) ? summary.Id : -1;

    private static int OptionalTextLength(ContentSummary summary) =>
        (summary.HeadLine?.Length ?? 0) + summary.Version.Tag.Length;

    [Fact]
    public void GuardedReads_CompileWithoutNullForgiving()
    {
        var found = ContentDeliveryResult<ContentSummary>.Found(ContentDeliveryContractTests.Summary(3));

        Assert.Equal(3, ReadGuardedByIsFound(found));
        Assert.Equal(3, ReadGuardedByTryGetValue(found));
        Assert.Equal(-1, ReadGuardedByIsFound(ContentDeliveryResult<ContentSummary>.NotFound()));
        Assert.Equal(-1, ReadGuardedByTryGetValue(ContentDeliveryResult<ContentSummary>.InvalidCulture()));
        Assert.Equal(2, OptionalTextLength(ContentDeliveryContractTests.Summary(3)));
    }
}
