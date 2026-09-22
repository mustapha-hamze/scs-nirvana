using System.Threading.Tasks;
using Domains.Entities.ContentManagement;

namespace Application.UseCases.TranslatorServices;

// Compatibility surface kept exactly as existing Web callers use it (Translate(content), no
// token) while adding an optional CancellationToken for callers that migrate to pass one.
public interface IContentTranslator
{
    Task<string> Translate(Content content, CancellationToken cancellationToken = default);
}