using System;
using System.Threading;
using System.Threading.Tasks;
using Application.UseCases.TranslatorServices;
using Domains.Entities.ContentManagement;
using Xunit;

namespace Core.Tests.TranslatorServices;

public class ContentTranslatorTests
{
    private class FakeTranslationPort : ITranslationPort
    {
        private readonly Func<TranslationRequest, CancellationToken, Task<TranslationResult>> _handler;
        public FakeTranslationPort(Func<TranslationRequest, CancellationToken, Task<TranslationResult>> handler) => _handler = handler;

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
            => _handler(request, cancellationToken);
    }

    [Fact]
    public async Task Translate_Success_ReturnsTranslatedJson()
    {
        var port = new FakeTranslationPort((request, ct) => Task.FromResult(TranslationResult.Ok("{\"title\":\"ترجمه\"}")));
        var sut = new ContentTranslator(port);

        var result = await sut.Translate(new Content { Title = "Hello" });

        Assert.Equal("{\"title\":\"ترجمه\"}", result);
    }

    [Fact]
    public async Task Translate_InvalidModelJson_ThrowsWithError()
    {
        var port = new FakeTranslationPort((request, ct) =>
            Task.FromResult(TranslationResult.Failed("Model response was not valid JSON.")));
        var sut = new ContentTranslator(port);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Translate(new Content { Title = "Hello" }));
        Assert.Equal("Model response was not valid JSON.", ex.Message);
    }

    [Fact]
    public async Task Translate_Cancelled_PropagatesCancellation()
    {
        var port = new FakeTranslationPort((request, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(TranslationResult.Ok("unreachable"));
        });
        var sut = new ContentTranslator(port);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.Translate(new Content { Title = "Hello" }, cts.Token));
    }

    [Fact]
    public async Task Translate_MissingConfiguration_ThrowsWithError()
    {
        // Simulates the real adapter surfacing a missing OPENAI_API_KEY as a failed result
        // (in production this fails earlier, at startup, via OpenAiTranslationOptions'
        // [Required] validation - this proves the compatibility adapter treats that failure
        // the same as any other translation failure rather than special-casing it).
        var port = new FakeTranslationPort((request, ct) =>
            Task.FromResult(TranslationResult.Failed("OPENAI_API_KEY is not configured.")));
        var sut = new ContentTranslator(port);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Translate(new Content { Title = "Hello" }));
        Assert.Equal("OPENAI_API_KEY is not configured.", ex.Message);
    }
}
