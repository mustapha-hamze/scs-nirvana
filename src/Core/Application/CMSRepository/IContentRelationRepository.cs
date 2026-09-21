using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.CMSRepository;

// Relation commands: replaces Content's Category/Tag/Culture join rows and the legacy
// pipe-delimited compatibility string together. Each method only stages the change - the
// caller (Application layer) is responsible for running it inside one transaction/SaveChanges
// so the join rows and the compatibility string always change together.
public interface IContentRelationRepository
{
    Task CreateContentCategories(int contentId, List<int> categoryIds);
    Task CreateContentTags(int contentId, List<int> tagIds);
    Task CreateContentCultures(int contentId, List<int> cultureIds);
}
