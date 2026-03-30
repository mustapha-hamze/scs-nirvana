using System.Threading.Tasks;
using Domains.Entities.ContentManagement;

namespace Core.Services.TranslatorServices;

public interface IContentTranslator
{
    Task<string> Translate(Content content);
}