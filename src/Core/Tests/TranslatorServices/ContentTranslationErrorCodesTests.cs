using System.Reflection;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Xunit;

namespace Core.Tests.TranslatorServices;

public class ContentTranslationErrorCodesTests
{
    private const string Generic = "Translation could not be completed. Try again later.";

    // The approved operator-facing text for every stored code.
    public static TheoryData<string, string> Approved => new()
    {
        { ContentTranslationErrorCodes.ProviderRetriesExhausted, "Translation could not be completed after several attempts. Try again later." },
        { ContentTranslationErrorCodes.ProviderError, "Translation provider could not complete the request. Try again later." },
        { ContentTranslationErrorCodes.ProviderTimeout, "Translation timed out. Try again." },
        { ContentTranslationErrorCodes.ProviderCancelled, "Translation was interrupted. Try again." },
        { ContentTranslationErrorCodes.InvalidOutput, "The translation response could not be used. Try again." },
        { ContentTranslationErrorCodes.LeaseExpired, "Translation processing was interrupted. Try again." },
        { ContentTranslationErrorCodes.CultureUnavailable, "The target language is unavailable. Contact an administrator." },
        { ContentTranslationErrorCodes.TranslationDeleted, "The translation was removed before completion." },
        { ContentTranslationErrorCodes.ProviderRateLimited, Generic }
    };

    [Theory]
    [MemberData(nameof(Approved))]
    public void FailedJob_MapsEachCode_ToItsApprovedReason(string code, string reason) =>
        Assert.Equal(reason, ContentTranslationErrorCodes.FailureReasonFor(ContentTranslationJobState.Failed, code));

    [Fact]
    public void EveryDefinedCode_HasAnApprovedReason()
    {
        var defined = typeof(ContentTranslationErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name != nameof(ContentTranslationErrorCodes.GenericFailureReason))
            .Select(f => (string)f.GetRawConstantValue()!);

        Assert.Equal(defined.OrderBy(c => c), Approved.Select(row => (string)row[0]).OrderBy(c => c));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("secret-provider-detail {\"raw\":1}")]
    [InlineData("future_code")]
    [InlineData("PROVIDER_ERROR")]
    public void UnknownOrBlankCode_MapsToTheGenericReason_WithoutEchoingIt(string code)
    {
        var reason = ContentTranslationErrorCodes.FailureReasonFor(ContentTranslationJobState.Failed, code);

        Assert.Equal(Generic, reason);
        if (!string.IsNullOrWhiteSpace(code))
            Assert.DoesNotContain(code, reason);
    }

    [Theory]
    [InlineData(ContentTranslationJobState.Queued)]
    [InlineData(ContentTranslationJobState.Processing)]
    [InlineData(ContentTranslationJobState.Succeeded)]
    [InlineData(ContentTranslationJobState.Superseded)]
    public void NonFailedJob_HasNoReason_WhateverCodeIsStored(ContentTranslationJobState state)
    {
        Assert.Null(ContentTranslationErrorCodes.FailureReasonFor(state, ContentTranslationErrorCodes.ProviderError));
        Assert.Null(ContentTranslationErrorCodes.FailureReasonFor(state, "secret"));
    }
}
