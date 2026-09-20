using System.Threading;
using System.Threading.Tasks;

namespace Application.UseCases.TranslatorServices;

// Application-level port for the translation provider. The concrete OpenAI adapter lives in
// Infrastructure; Application only ever depends on this contract.
public interface ITranslationPort
{
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
}
